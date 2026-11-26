use std::collections::HashMap;
use std::net::SocketAddr;
use std::path::{Component, Path, PathBuf};

use axum::body::Bytes;
use axum::extract::{ConnectInfo, OriginalUri, Path as AxumPath, Query, State};
use axum::http::{header, HeaderMap, HeaderValue, StatusCode};
use axum::middleware::{self, Next};
use axum::response::{Html, IntoResponse, Response};
use axum::routing::{delete, get, post};
use axum::{Json, Router};
use serde::Deserialize;
use serde_json::{json, Value};
use uuid::Uuid;

use crate::db::CharacterRecord;
use crate::state::{DirectoryServerEntry, ServerState};
use crate::terrain::DEFAULT_TERRAIN_ID;

#[derive(Clone)]
struct JoinPasswordExt(String);

async fn inject_path_password(
    OriginalUri(orig): OriginalUri,
    mut req: axum::http::Request<axum::body::Body>,
    next: Next,
) -> Response {
    if let Some(rest) = orig.path().strip_prefix("/p/") {
        if let Some((raw_pw, _)) = rest.split_once('/') {
            let decoded = percent_decode(raw_pw, false);
            if !decoded.is_empty() {
                req.extensions_mut().insert(JoinPasswordExt(decoded));
            }
        }
    }
    next.run(req).await
}

fn build_game_routes() -> Router<ServerState> {
    Router::new()
        .route("/servers.json", get(get_servers_json))
        .route("/status", get(get_status))
        .route("/health", get(get_health))
        .route("/knock", get(get_knock))
        .route("/notice", get(get_notice))
        .route("/events/calendar", get(get_events_calendar))
        .route("/season/pass", get(get_season_pass).post(get_season_pass))
        .route("/season/claim", get(post_season_claim).post(post_season_claim))
        .route("/cs/answer", get(get_cs_answer))
        .route("/live/:platform/Info.5.2.1.json", get(get_bundle_info))
        .route("/accounts", post(post_accounts))
        .route("/sessions", post(post_sessions))
        .route("/players", get(get_players_search).post(post_players))
        .route("/players/:entity_id", get(get_player_info).delete(delete_player))
        .route(
            "/players/:entity_id/cancel_player_deletion",
            post(cancel_player_deletion),
        )
        .route("/admission", get(get_admission))
        .route("/entry", get(get_entry))
        .route("/assets/*path", get(get_asset_file))
        .route("/terrains/*path", get(get_terrain_route))
}

pub fn build_http_router(state: ServerState) -> Router {
    let game_routes = build_game_routes();
    let pw_prefixed = build_game_routes().route_layer(middleware::from_fn(inject_path_password));

    let admin_routes = Router::new()
        .route("/metrics", get(get_prometheus_metrics))
        .route("/admin", get(get_admin_html))
        .route("/admin/snapshot", get(admin_snapshot))
        .route("/admin/players", get(admin_list_players))
        .route("/admin/logs", get(admin_list_logs))
        .route("/admin/logs/export", get(admin_export_logs))
        .route("/admin/bans", get(admin_list_bans).post(admin_add_ban))
        .route("/admin/bans/:id", delete(admin_delete_ban))
        .route("/admin/kick", post(admin_kick_player))
        .route("/admin/config", get(admin_get_config).post(admin_update_config))
        .route(
            "/admin/directory/:server_id",
            delete(admin_remove_directory_server),
        )
        .layer(middleware::from_fn(admin_cors_layer));

    Router::new()
        .merge(game_routes)
        .nest("/p/:join_pw", pw_prefixed)
        // Community Server Directory (Source of Truth) & Attestation
        .route("/api/attestation", get(get_api_attestation))
        .route("/directory", get(get_directory_html))
        .route("/api/directory/servers", get(get_directory_servers))
        .route("/api/directory/challenge", get(get_directory_challenge))
        .route(
            "/api/directory/heartbeat",
            post(post_directory_heartbeat).delete(delete_directory_heartbeat),
        )
        .merge(admin_routes)
        .with_state(state)
}

fn allowed_admin_origin(headers: &HeaderMap) -> Option<HeaderValue> {
    let origin = headers.get(header::ORIGIN)?.to_str().ok()?.trim();
    if origin.is_empty() {
        return None;
    }
    if let Ok(configured) = std::env::var("DURANGO_ADMIN_CORS_ORIGIN") {
        let cfg = configured.trim();
        if !cfg.is_empty() && origin.eq_ignore_ascii_case(cfg) {
            return HeaderValue::from_str(origin).ok();
        }
    }
    let after_scheme = origin
        .strip_prefix("http://")
        .or_else(|| origin.strip_prefix("https://"))?;
    let host_port = after_scheme.split('/').next()?;
    if host_port.starts_with('[') {
        if host_port == "[::1]" || host_port.starts_with("[::1]:") {
            return HeaderValue::from_str(origin).ok();
        }
    } else {
        let host = host_port.split(':').next().unwrap_or("");
        if host.eq_ignore_ascii_case("127.0.0.1") || host.eq_ignore_ascii_case("localhost") {
            return HeaderValue::from_str(origin).ok();
        }
        if let Some(req_host) = headers
            .get(header::HOST)
            .and_then(|v| v.to_str().ok())
            .map(host_without_port)
        {
            if !host.is_empty() && host.eq_ignore_ascii_case(req_host) {
                return HeaderValue::from_str(origin).ok();
            }
        }
    }
    None
}

async fn admin_cors_layer(
    req: axum::http::Request<axum::body::Body>,
    next: Next,
) -> Response {
    let allowed = allowed_admin_origin(req.headers());
    if req.method() == axum::http::Method::OPTIONS {
        let mut resp = if allowed.is_some() {
            StatusCode::NO_CONTENT.into_response()
        } else {
            StatusCode::FORBIDDEN.into_response()
        };
        let h = resp.headers_mut();
        h.insert(header::VARY, HeaderValue::from_static("Origin"));
        if let Some(origin) = allowed {
            h.insert(header::ACCESS_CONTROL_ALLOW_ORIGIN, origin);
            h.insert(
                header::ACCESS_CONTROL_ALLOW_METHODS,
                HeaderValue::from_static("GET, POST, DELETE, OPTIONS"),
            );
            h.insert(
                header::ACCESS_CONTROL_ALLOW_HEADERS,
                HeaderValue::from_static("Content-Type, X-Admin-Token, Authorization"),
            );
        }
        return resp;
    }
    let mut resp = next.run(req).await;
    let h = resp.headers_mut();
    h.insert(header::VARY, HeaderValue::from_static("Origin"));
    if let Some(origin) = allowed {
        h.insert(header::ACCESS_CONTROL_ALLOW_ORIGIN, origin);
        h.insert(
            header::ACCESS_CONTROL_ALLOW_METHODS,
            HeaderValue::from_static("GET, POST, DELETE, OPTIONS"),
        );
        h.insert(
            header::ACCESS_CONTROL_ALLOW_HEADERS,
            HeaderValue::from_static("Content-Type, X-Admin-Token, Authorization"),
        );
    }
    resp
}

fn extract_client_ip(
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: &HeaderMap,
) -> String {
    if let Some(ConnectInfo(addr)) = connect_info {
        return addr.ip().to_string();
    }
    if let Some(xff) = headers
        .get("x-forwarded-for")
        .and_then(|v| v.to_str().ok())
        .and_then(|s| s.split(',').next())
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        return xff.to_string();
    }
    "127.0.0.1".to_string()
}

fn extract_join_password<'a>(
    headers: &'a HeaderMap,
    form: Option<&'a HashMap<String, String>>,
    ext_pw: Option<&'a JoinPasswordExt>,
) -> Option<&'a str> {
    if let Some(s) = ext_pw
        .map(|e| e.0.as_str())
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        return Some(s);
    }
    if let Some(h) = headers
        .get("x-durango-join-password")
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        return Some(h);
    }
    form.and_then(|m| {
        m.get("join_password")
            .or_else(|| m.get("password"))
            .map(String::as_str)
            .map(str::trim)
            .filter(|s| !s.is_empty())
    })
}

fn login_gate_denied(message: &str) -> Response {
    (
        StatusCode::FORBIDDEN,
        Json(json!({
            "error": {
                "code": "web_login_required",
                "message": message
            }
        })),
    )
        .into_response()
}

#[allow(clippy::too_many_arguments)]
fn check_access_gate(
    state: &ServerState,
    headers: &HeaderMap,
    form: Option<&HashMap<String, String>>,
    ext_pw: Option<&JoinPasswordExt>,
    owner_key: Option<&str>,
    entity_id: Option<&str>,
    name: Option<&str>,
    client_ip: &str,
    session_already_unlocked: bool,
) -> Result<(), Response> {
    let now = ServerState::now_unix_secs();
    if let Some(ban) = state
        .db
        .check_ban(owner_key, entity_id, name, Some(client_ip), now)
    {
        let msg = if ban.reason.is_empty() {
            "You are banned from this community server.".to_string()
        } else {
            format!("You are banned from this server: {}", ban.reason)
        };
        tracing::warn!(
            %client_ip,
            ?owner_key,
            ban_type = %ban.ban_type,
            reason = %ban.reason,
            "HTTP access denied: banned"
        );
        state.db.log_audit(
            now,
            "MODERATION",
            owner_key.map(crate::state::format_account_tag).as_deref().unwrap_or("unknown"),
            client_ip,
            "",
            &format!("Blocked banned login ({}): {}", ban.ban_type, ban.reason),
        );
        return Err(login_gate_denied(&msg));
    }

    if state.effective_join_password().is_some() && !session_already_unlocked {
        let provided = extract_join_password(headers, form, ext_pw);
        if !state.verify_join_password(provided) {
            state.db.log_audit(
                now,
                "AUTH",
                owner_key.map(crate::state::format_account_tag).as_deref().unwrap_or("unknown"),
                client_ip,
                "",
                "Rejected login: missing or invalid server join password",
            );
            return Err(login_gate_denied(
                "This server requires a join password. Enter it in the Durango-CE password prompt or connect via /p/<password>.",
            ));
        }
    }
    Ok(())
}

fn verify_admin_request(
    state: &ServerState,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: &HeaderMap,
) -> Result<(), Response> {
    let client_ip = extract_client_ip(connect_info, headers);
    let Some(expected) = state
        .config
        .admin_token
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    else {
        // Without DURANGO_ADMIN_TOKEN, restrict /admin/* strictly to direct loopback
        // (never trust loopback if proxy forwarding headers are present).
        let has_proxy_hdr = headers.contains_key("x-forwarded-for") || headers.contains_key("forwarded");
        let is_loopback = client_ip
            .parse::<std::net::IpAddr>()
            .is_ok_and(|ip| ip.to_canonical().is_loopback());
        if !has_proxy_hdr && is_loopback {
            return Ok(());
        }
        return Err((
            StatusCode::FORBIDDEN,
            Json(json!({ "error": "admin_token_not_configured" })),
        )
            .into_response());
    };

    let provided = headers
        .get("x-admin-token")
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| extract_auth_token(headers));

    let a = expected.as_bytes();
    let b = provided.as_bytes();
    if a.len() == b.len() && a.iter().zip(b.iter()).fold(0u8, |acc, (&x, &y)| acc | (x ^ y)) == 0 {
        Ok(())
    } else {
        Err((
            StatusCode::UNAUTHORIZED,
            Json(json!({ "error": "invalid_admin_token" })),
        )
            .into_response())
    }
}

fn format_host_port(host: &str, port: impl std::fmt::Display) -> String {
    if host.contains(':') && !host.starts_with('[') {
        format!("[{host}]:{port}")
    } else {
        format!("{host}:{port}")
    }
}

fn root_url(headers: &HeaderMap, state: &ServerState) -> String {
    let scheme = headers
        .get("x-forwarded-proto")
        .and_then(|v| v.to_str().ok())
        .and_then(|s| s.split(',').next())
        .map(str::trim)
        .filter(|s| s.eq_ignore_ascii_case("https") || s.eq_ignore_ascii_case("http"))
        .map(|s| if s.eq_ignore_ascii_case("https") { "https" } else { "http" })
        .unwrap_or("http");
    if let Some(host) = headers.get(header::HOST).and_then(|v| v.to_str().ok()) {
        let trimmed = host.trim();
        if !trimmed.is_empty() {
            return format!("{scheme}://{trimmed}");
        }
    }
    let port = state
        .config
        .http_bind
        .rsplit(':')
        .next()
        .unwrap_or("8190");
    format!(
        "{scheme}://{}",
        format_host_port(&state.config.public_host, port)
    )
}

fn host_without_port(raw_host: &str) -> &str {
    let trimmed = raw_host.trim();
    if trimmed.starts_with('[') {
        if let Some(end) = trimmed.find(']') {
            return &trimmed[..=end];
        }
    }
    trimmed.split(':').next().unwrap_or(trimmed)
}

fn ct_eq(a: &str, b: &str) -> bool {
    let ab = a.as_bytes();
    let bb = b.as_bytes();
    if ab.len() != bb.len() {
        return false;
    }
    ab.iter().zip(bb.iter()).fold(0u8, |acc, (&x, &y)| acc | (x ^ y)) == 0
}

fn normalize_account_key(raw: Option<&str>) -> Option<String> {
    crate::state::canonical_account_id_from_secret(raw?)
}

fn extract_auth_token(headers: &HeaderMap) -> &str {
    let raw = headers
        .get(header::AUTHORIZATION)
        .and_then(|v| v.to_str().ok())
        .map(str::trim)
        .unwrap_or("");
    if let Some(rest) = raw.strip_prefix("Bearer ").or_else(|| raw.strip_prefix("bearer ")) {
        return rest.trim();
    }
    if let Some(rest) = raw.strip_prefix("Token ").or_else(|| raw.strip_prefix("token ")) {
        return rest.trim();
    }
    raw
}

fn parse_multipart(headers: &HeaderMap, text: &str) -> Option<HashMap<String, String>> {
    let ct_boundary = headers
        .get(header::CONTENT_TYPE)
        .and_then(|v| v.to_str().ok())
        .and_then(|ct| {
            let lower = ct.to_ascii_lowercase();
            let pos = lower.find("boundary=")?;
            let rest = ct[pos + "boundary=".len()..].trim();
            let b = rest.split(';').next()?.trim().trim_matches('"');
            if b.is_empty() { None } else { Some(b.to_string()) }
        });

    let boundary = ct_boundary.or_else(|| {
        let first_line = text.lines().next()?.trim();
        if first_line.starts_with("--") && first_line.len() > 4 {
            Some(first_line[2..].to_string())
        } else {
            None
        }
    })?;

    let delim = format!("--{boundary}");
    let mut fields = HashMap::new();
    for part in text.split(&delim) {
        let (hdr_part, val_raw) = if let Some((h, v)) = part.split_once("\r\n\r\n") {
            (h, v)
        } else if let Some((h, v)) = part.split_once("\n\n") {
            (h, v)
        } else {
            continue;
        };
        let lower_hdr = hdr_part.to_ascii_lowercase();
        let name_pos = lower_hdr.match_indices("name=\"").find_map(|(idx, _)| {
            if idx == 0
                || matches!(
                    lower_hdr.as_bytes()[idx - 1],
                    b';' | b' ' | b'\t' | b'\r' | b'\n'
                )
            {
                Some(idx)
            } else {
                None
            }
        });
        let Some(name_pos) = name_pos else {
            continue;
        };
        let start = name_pos + "name=\"".len();
        let Some(end_rel) = hdr_part[start..].find('"') else {
            continue;
        };
        let name = hdr_part[start..start + end_rel].to_string();
        let val = val_raw
            .strip_suffix("\r\n")
            .or_else(|| val_raw.strip_suffix('\n'))
            .unwrap_or(val_raw)
            .to_string();
        fields.insert(name, val);
    }
    if fields.is_empty() { None } else { Some(fields) }
}

fn parse_form_or_json(headers: &HeaderMap, body: &[u8]) -> HashMap<String, String> {
    let mut map = HashMap::new();
    let Ok(text) = std::str::from_utf8(body) else {
        return map;
    };
    if let Some(mp) = parse_multipart(headers, text) {
        return mp;
    }
    let trimmed = text.trim();
    if trimmed.starts_with('{') {
        if let Ok(Value::Object(obj)) = serde_json::from_str::<Value>(trimmed) {
            for (k, v) in obj {
                let s = match v {
                    Value::String(s) => s,
                    other => other.to_string(),
                };
                map.insert(k, s);
            }
            return map;
        }
    }
    for pair in trimmed.split('&') {
        if let Some((k, v)) = pair.split_once('=') {
            map.insert(percent_decode(k, true), percent_decode(v, true));
        }
    }
    map
}

fn percent_decode(input: &str, plus_as_space: bool) -> String {
    let bytes = input.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b'+' if plus_as_space => {
                out.push(b' ');
                i += 1;
            }
            b'%' if i + 2 < bytes.len() => {
                if let (Some(hi), Some(lo)) = (hex_val(bytes[i + 1]), hex_val(bytes[i + 2])) {
                    out.push((hi << 4) | lo);
                    i += 3;
                } else {
                    out.push(b'%');
                    i += 1;
                }
            }
            b => {
                out.push(b);
                i += 1;
            }
        }
    }
    String::from_utf8_lossy(&out).into_owned()
}

fn hex_val(b: u8) -> Option<u8> {
    match b {
        b'0'..=b'9' => Some(b - b'0'),
        b'a'..=b'f' => Some(b - b'a' + 10),
        b'A'..=b'F' => Some(b - b'A' + 10),
        _ => None,
    }
}

async fn get_servers_json(State(state): State<ServerState>, headers: HeaderMap) -> Json<Value> {
    let base = root_url(&headers, &state);
    let now = ServerState::now_unix_secs();
    let live = state.list_live_directory_servers(&base, now).await;
    tracing::info!(count = live.len(), "HTTP GET /servers.json -> {base}");
    let servers: Vec<Value> = live
        .into_iter()
        .map(|s| {
            let display_name = if s.password_required && !s.name.contains("[Locked]") {
                format!("{} [Locked]", s.name)
            } else {
                s.name
            };
            let mut obj = json!({
                "name": display_name,
                "gateway": s.gateway,
                "region": s.region,
                "online": s.online_players,
                "max_players": s.max_players,
                "password_required": s.password_required,
                "version": s.version
            });
            if s.verified == Some(true) {
                if let Some(m) = obj.as_object_mut() {
                    m.insert("verified".to_string(), json!(true));
                }
            }
            obj
        })
        .collect();
    Json(json!({ "servers": servers }))
}

async fn get_status(State(state): State<ServerState>) -> Json<Value> {
    let online = state.live_clients.read().await.len();
    let password_required = state.effective_join_password().is_some();
    tracing::info!(online, password_required, "HTTP GET /status -> 200");
    Json(json!({
        "ok": true,
        "online": online,
        "max_players": state.config.max_players,
        "name": state.effective_server_name(),
        "region": state.config.region_tag,
        "password_required": password_required,
        "version": "3.3.6",
        "server_mode": state.config.server_mode
    }))
}

async fn get_health(State(state): State<ServerState>) -> Json<Value> {
    let online = state.live_clients.read().await.len();
    let sessions = state.sessions.read().await.len();
    Json(json!({
        "ok": true,
        "engine": "durango-server-rs",
        "version": env!("CARGO_PKG_VERSION"),
        "name": state.effective_server_name(),
        "players_online": online,
        "sessions": sessions,
        "password_required": state.effective_join_password().is_some(),
        "tcp_port": state.game_tcp_port
    }))
}

#[derive(Deserialize)]
struct KnockQuery {
    platform: Option<String>,
    version: Option<String>,
}

async fn get_knock(
    State(state): State<ServerState>,
    headers: HeaderMap,
    Query(q): Query<KnockQuery>,
) -> Json<Value> {
    let platform = match q.platform.as_deref().map(|s| s.to_ascii_lowercase()).as_deref() {
        Some("android") => "android",
        Some("ios") => "ios",
        _ => "windows",
    };
    let root = root_url(&headers, &state);
    tracing::info!(platform, version = ?q.version, "HTTP GET /knock -> 200");
    Json(json!({
        "server_version": "5.2.1",
        "compatible": true,
        "download_url": "",
        "assetbundle_index_url": format!("{root}/live/{platform}/Info.5.2.1.json"),
        "assetbundle_url_root": format!("{root}/live/{platform}/"),
        "server_mode": state.config.server_mode,
        "password_required": state.effective_join_password().is_some(),
        "cheats_allowed": state.config.server_mode.eq_ignore_ascii_case("sandbox")
    }))
}

async fn get_notice(State(state): State<ServerState>) -> Json<Value> {
    let notice = state.effective_notice();
    if notice.trim().is_empty() {
        Json(json!({}))
    } else {
        Json(json!({ "notice": notice }))
    }
}

async fn get_events_calendar() -> Json<Value> {
    Json(json!({ "ok": true, "events": [] }))
}

async fn get_season_pass(_body: Bytes) -> Json<Value> {
    Json(json!({ "ok": true, "active": false, "seasons": [] }))
}

async fn post_season_claim(_body: Bytes) -> Json<Value> {
    Json(json!({ "ok": false, "reason": "no_season" }))
}

async fn get_cs_answer() -> Json<Value> {
    Json(json!({ "updated_at": Value::Null }))
}

async fn get_bundle_info(AxumPath(platform): AxumPath<String>) -> Json<Value> {
    tracing::info!(%platform, "HTTP GET /live/{platform}/Info.5.2.1.json -> 200");
    Json(json!({
        "PreloadHash": "c4408d73cff6e7ad670c0c93a2b04385",
        "PreloadCrc": "c4408d73cff6e7ad670c0c93a2b04385",
        "ItemList": [],
        "FileList": []
    }))
}

async fn post_accounts(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    ext_pw: Option<axum::Extension<JoinPasswordExt>>,
    body: Bytes,
) -> Response {
    let form = parse_form_or_json(&headers, &body);
    let client_ip = extract_client_ip(connect_info, &headers);
    let raw_acct = form.get("account_id").map(|s| s.trim());
    let owner_opt = normalize_account_key(raw_acct);
    if raw_acct.is_some_and(|s| !s.is_empty()) && owner_opt.is_none() {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "invalid_account_key" })),
        )
            .into_response();
    }
    if let Err(resp) = check_access_gate(
        &state,
        &headers,
        Some(&form),
        ext_pw.as_ref().map(|e| &e.0),
        owner_opt.as_deref(),
        None,
        None,
        &client_ip,
        false,
    ) {
        return resp;
    }
    let Some(owner_key) = owner_opt else {
        return Json(json!({
            "players": [],
            "player_slot_count": crate::state::DEFAULT_PLAYER_SLOTS,
            "max_player_slot_count": crate::state::DEFAULT_PLAYER_SLOTS
        }))
        .into_response();
    };
    let chars = state.db.list_by_owner(&owner_key).unwrap_or_default();
    let slots = crate::state::DEFAULT_PLAYER_SLOTS.max(chars.len());
    let players: Vec<Value> = chars
        .iter()
        .map(|c| {
            json!({
                "player_level": c.level,
                "disconnected_at": c.disconnected_at,
                "player_name": c.name,
                "player_entity_id": c.entity_id,
                "deletes_at": Value::Null
            })
        })
        .collect();
    tracing::info!(
        owner = &owner_key[..owner_key.len().min(8)],
        count = players.len(),
        "HTTP POST /accounts -> 200"
    );
    Json(json!({
        "players": players,
        "player_slot_count": slots,
        "max_player_slot_count": slots,
        "account_id": owner_key,
        "discord_name": crate::state::format_account_tag(&owner_key)
    }))
    .into_response()
}

async fn post_sessions(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    ext_pw: Option<axum::Extension<JoinPasswordExt>>,
    body: Bytes,
) -> Response {
    let form = parse_form_or_json(&headers, &body);
    let client_ip = extract_client_ip(connect_info, &headers);
    let Some(owner_key) = normalize_account_key(form.get("account_id").map(String::as_str)) else {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "invalid_or_missing_account_key" })),
        )
            .into_response();
    };
    if let Err(resp) = check_access_gate(
        &state,
        &headers,
        Some(&form),
        ext_pw.as_ref().map(|e| &e.0),
        Some(&owner_key),
        None,
        None,
        &client_ip,
        false,
    ) {
        return resp;
    }
    let (entity_id, token) = state.issue_session_with_auth(&owner_key, true).await;
    state.db.log_audit(
        ServerState::now_unix_secs(),
        "AUTH",
        &owner_key,
        &client_ip,
        "",
        &format!("Authenticated session (user_id={entity_id})"),
    );
    tracing::info!(
        owner = &owner_key[..owner_key.len().min(8)],
        %entity_id,
        %client_ip,
        "HTTP POST /sessions -> 200"
    );
    (
        StatusCode::OK,
        Json(json!({
            "user_id": entity_id,
            "session_token": token
        })),
    )
        .into_response()
}

#[derive(Deserialize)]
struct PlayerSearchQuery {
    name: Option<String>,
}

async fn get_players_search(
    State(state): State<ServerState>,
    Query(q): Query<PlayerSearchQuery>,
) -> Json<Value> {
    let needle = q.name.as_deref().unwrap_or("").trim();
    if needle.is_empty() {
        return Json(json!({ "players": [] }));
    }
    let found = state
        .db
        .search_characters_with_clans(needle)
        .unwrap_or_default();
    let list: Vec<Value> = found
        .into_iter()
        .map(|(ch, clan)| {
            let (clan_id, clan_name) = clan
                .map(|(cid, cname, _)| (cid, cname))
                .unwrap_or_default();
            json!({
                "entity_id": ch.entity_id,
                "freq": 0,
                "name": ch.name,
                "level": ch.level,
                "clan": { "clan_id": clan_id, "clan_name": clan_name },
                "personal_region_id": ""
            })
        })
        .collect();
    Json(json!({ "players": list }))
}

async fn post_players(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    ext_pw: Option<axum::Extension<JoinPasswordExt>>,
    body: Bytes,
) -> Response {
    let client_ip = extract_client_ip(connect_info, &headers);
    let token = extract_auth_token(&headers);
    let session = state.get_valid_session(token).await;
    let Some(session) = session else {
        tracing::warn!(
            has_auth = !token.is_empty(),
            body_len = body.len(),
            "HTTP POST /players -> 401 unauthorized (missing/unknown session token)"
        );
        return (
            StatusCode::UNAUTHORIZED,
            Json(json!({ "error": "unauthorized" })),
        )
            .into_response();
    };

    let form = parse_form_or_json(&headers, &body);
    let sess_eid = if session.entity_id.is_empty() {
        None
    } else {
        Some(session.entity_id.as_str())
    };
    if let Err(resp) = check_access_gate(
        &state,
        &headers,
        Some(&form),
        ext_pw.as_ref().map(|e| &e.0),
        Some(&session.owner_key),
        sess_eid,
        None,
        &client_ip,
        session.password_ok,
    ) {
        return resp;
    }

    let existing_chars = state.db.list_by_owner(&session.owner_key).unwrap_or_default();
    if existing_chars.len() >= crate::state::DEFAULT_PLAYER_SLOTS {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "character_slot_limit_reached" })),
        )
            .into_response();
    }

    let raw_name = form.get("name").map(|s| s.trim()).unwrap_or("");
    let char_name = if raw_name.is_empty() {
        format!("Pioneer-{}", &Uuid::new_v4().simple().to_string()[..4])
    } else {
        let len = raw_name.chars().count();
        if len > 24 || raw_name.chars().any(char::is_control) {
            return (
                StatusCode::BAD_REQUEST,
                Json(json!({ "error": "invalid_character_name" })),
            )
                .into_response();
        }
        raw_name.to_string()
    };
    if state
        .db
        .character_name_exists(&char_name)
        .unwrap_or(false)
    {
        return (
            StatusCode::CONFLICT,
            Json(json!({ "error": "character_name_taken" })),
        )
            .into_response();
    }

    let model_info = form.get("model_info").cloned().unwrap_or_default();
    if model_info.len() > 4096 {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "model_info_too_large" })),
        )
            .into_response();
    }

    let job: i32 = form
        .get("job")
        .and_then(|s| s.parse().ok())
        .unwrap_or(0)
        .clamp(0, 7);
    let model_outfit = serde_json::from_str::<Value>(&model_info)
        .ok()
        .and_then(|v| v["starter_outfit"].as_i64())
        .map(|n| n as i32);
    let starter_outfit: usize = form
        .get("starter_outfit")
        .and_then(|s| s.parse::<i32>().ok())
        .or(model_outfit)
        .filter(|&v| (0..=7).contains(&v))
        .unwrap_or(job) as usize;
    let region = form
        .get("region")
        .map(|s| s.trim())
        .filter(|s| !s.is_empty())
        .unwrap_or(DEFAULT_TERRAIN_ID);

    let entity_id = if existing_chars.is_empty()
        && !session.entity_id.is_empty()
        && state
            .db
            .get_by_entity_id(&session.entity_id)
            .ok()
            .flatten()
            .is_none()
    {
        session.entity_id.clone()
    } else {
        Uuid::new_v4().to_string()
    };

    let gender = if form
        .get("gender")
        .is_some_and(|g| g.eq_ignore_ascii_case("female"))
    {
        "female".to_string()
    } else {
        "male".to_string()
    };

    let now = ServerState::now_unix_secs();
    let rec = CharacterRecord {
        entity_id: entity_id.clone(),
        owner_key: session.owner_key.clone(),
        name: char_name,
        job,
        level: 1,
        region_id: crate::state::DEFAULT_REGION_CODE.to_string(),
        terrain_id: DEFAULT_TERRAIN_ID.to_string(),
        pos_x: 63.0,
        pos_y: 71.0,
        disconnected_at: 0.0,
        created_at: now,
        gender,
        model_info,
        exp: 0,
        untrained_count: 0,
        life: 100.0,
        stamina: 100.0,
        energy: 100.0,
        fatigue: 0.0,
    };
    if let Err(err) = state.db.upsert_character(&rec) {
        tracing::error!(%err, "sqlite insert character failed");
        return (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(json!({ "error": "db_write_failed" })),
        )
            .into_response();
    }
    state.temp_characters.write().await.remove(&entity_id);

    // Grant and equip starter outfit matching selected outfit/job (matching Gateway.cs)
    const STARTER_OUTFITS: [&str; 8] = [
        "clothes_engineer",
        "clothes_officeworker",
        "clothes_student",
        "clothes_farmer",
        "clothes_waiter",
        "clothes_soldier",
        "clothes_homeworker",
        "clothes_jobless",
    ];
    let outfit_proto = STARTER_OUTFITS[starter_outfit.min(7)];
    let (outfit_name, outfit_icon, outfit_size) = state.catalog.prototype_info(outfit_proto);
    let outfit_item_id = Uuid::new_v4().to_string();
    let outfit_rec = crate::db::ItemRecord {
        item_id: outfit_item_id.clone(),
        entity_id: entity_id.clone(),
        prototype_id: outfit_proto.to_string(),
        name: outfit_name,
        icon: outfit_icon,
        level: 1,
        size: outfit_size,
        durability: 100.0,
        max_durability: 100.0,
        locked: false,
        collectible_id: None,
        generator_id: None,
        created_at: now,
    };
    let _ = state.db.insert_items(std::slice::from_ref(&outfit_rec));
    let _ = state.db.set_equipped(&entity_id, "body", &outfit_item_id);

    let _ = state.bind_session_to_entity(token, &entity_id).await;
    state.db.log_audit(
        now,
        "AUTH",
        &rec.name,
        &client_ip,
        &rec.region_id,
        &format!(
            "Created character '{}' ({}, job={}, outfit={})",
            rec.name,
            crate::state::format_account_tag(&rec.owner_key),
            job,
            outfit_proto
        ),
    );
    tracing::info!(
        %entity_id,
        name = %rec.name,
        job,
        requested_region = region,
        outfit = outfit_proto,
        "HTTP POST /players -> 200 (persisted to SQLite)"
    );
    (StatusCode::OK, Json(json!({ "entity_id": entity_id }))).into_response()
}

async fn get_player_info(
    State(state): State<ServerState>,
    AxumPath(entity_id): AxumPath<String>,
) -> Response {
    let Some(ch) = state.resolve_character(&entity_id).await else {
        return (StatusCode::NOT_FOUND, Json(json!({}))).into_response();
    };
    let (clan_id, clan_name) = state
        .db
        .get_player_clan(&ch.entity_id)
        .ok()
        .flatten()
        .map(|(cid, cname, _)| (cid, cname))
        .unwrap_or_default();
    let personal_region_id = state
        .db
        .get_player_points(&ch.entity_id)
        .ok()
        .and_then(|p| p.personal_region_id)
        .unwrap_or_default();
    (
        StatusCode::OK,
        Json(json!({
            "entity_id": ch.entity_id,
            "freq": 0,
            "name": ch.name,
            "level": ch.level,
            "clan": { "clan_id": clan_id, "clan_name": clan_name },
            "personal_region_id": personal_region_id
        })),
    )
        .into_response()
}

async fn delete_player(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    AxumPath(entity_id): AxumPath<String>,
) -> Response {
    let client_ip = extract_client_ip(connect_info, &headers);
    let token = extract_auth_token(&headers);
    let session = state.get_valid_session(token).await;
    let Some(session) = session else {
        return (
            StatusCode::UNAUTHORIZED,
            Json(json!({ "error": "unauthorized" })),
        )
            .into_response();
    };
    let resolved = state.resolve_character(&entity_id).await;
    if let Some(ch) = &resolved {
        if !ch.owner_key.eq_ignore_ascii_case(&session.owner_key) {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "forbidden" })),
            )
                .into_response();
        }
    }
    let old_name = resolved
        .map(|c| c.name)
        .unwrap_or_else(|| entity_id.clone());
    let deleted_db = state
        .db
        .delete_character(&entity_id, &session.owner_key)
        .unwrap_or(false);
    let deleted_temp = {
        let mut temps = state.temp_characters.write().await;
        if temps
            .get(&entity_id)
            .is_some_and(|c| c.owner_key.eq_ignore_ascii_case(&session.owner_key))
        {
            temps.remove(&entity_id).is_some()
        } else {
            false
        }
    };
    let deleted = deleted_db || deleted_temp;
    if deleted {
        let _ = state.kick_by_entity(&entity_id).await;
        let fallback_eid = state
            .db
            .list_by_owner(&session.owner_key)
            .ok()
            .and_then(|mut v| v.drain(..).next().map(|c| c.entity_id))
            .unwrap_or_else(|| Uuid::new_v4().to_string());
        let mut rebound_tokens = Vec::new();
        {
            let mut sessions = state.sessions.write().await;
            for (tok_k, s) in sessions.iter_mut() {
                if s.entity_id == entity_id {
                    s.entity_id = fallback_eid.clone();
                    rebound_tokens.push(tok_k.clone());
                }
            }
        }
        for tok_k in rebound_tokens {
            let _ = state.db.update_session_entity(&tok_k, &fallback_eid);
        }
        state.db.log_audit(
            ServerState::now_unix_secs(),
            "AUTH",
            &old_name,
            &client_ip,
            "",
            &format!(
                "Deleted character '{}' ({})",
                old_name,
                crate::state::format_account_tag(&session.owner_key)
            ),
        );
    }
    tracing::info!(%entity_id, deleted, "HTTP DELETE /players/:entity_id -> 200");
    (StatusCode::OK, Json(json!({ "deletes_at": Value::Null }))).into_response()
}

async fn cancel_player_deletion(
    State(state): State<ServerState>,
    headers: HeaderMap,
    AxumPath(entity_id): AxumPath<String>,
    _body: Bytes,
) -> Response {
    let token = extract_auth_token(&headers);
    let Some(session) = state.get_valid_session(token).await else {
        return (
            StatusCode::UNAUTHORIZED,
            Json(json!({ "error": "unauthorized" })),
        )
            .into_response();
    };
    if let Some(ch) = state.resolve_character(&entity_id).await {
        if !ch.owner_key.eq_ignore_ascii_case(&session.owner_key) {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "forbidden" })),
            )
                .into_response();
        }
    }
    tracing::info!(%entity_id, "HTTP POST /players/:entity_id/cancel_player_deletion -> 200");
    (StatusCode::OK, Json(json!({ "ok": true }))).into_response()
}

async fn get_admission(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    ext_pw: Option<axum::Extension<JoinPasswordExt>>,
) -> Response {
    let client_ip = extract_client_ip(connect_info, &headers);
    let tok = extract_auth_token(&headers);
    let sess = if tok.is_empty() {
        None
    } else {
        state.get_valid_session(tok).await
    };
    if let Err(resp) = check_access_gate(
        &state,
        &headers,
        None,
        ext_pw.as_ref().map(|e| &e.0),
        sess.as_ref().map(|s| s.owner_key.as_str()),
        sess.as_ref().map(|s| s.entity_id.as_str()),
        None,
        &client_ip,
        sess.as_ref().is_some_and(|s| s.password_ok),
    ) {
        return resp;
    }
    Json(json!({ "admitted": true })).into_response()
}

#[derive(Deserialize)]
struct EntryQuery {
    entity_id: Option<String>,
}

async fn get_entry(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    ext_pw: Option<axum::Extension<JoinPasswordExt>>,
    Query(q): Query<EntryQuery>,
) -> Response {
    let client_ip = extract_client_ip(connect_info, &headers);
    let tok = extract_auth_token(&headers);
    let sess = if tok.is_empty() {
        None
    } else {
        state.get_valid_session(tok).await
    };
    let target_eid = q
        .entity_id
        .as_deref()
        .or_else(|| sess.as_ref().map(|s| s.entity_id.as_str()));
    if let Err(resp) = check_access_gate(
        &state,
        &headers,
        None,
        ext_pw.as_ref().map(|e| &e.0),
        sess.as_ref().map(|s| s.owner_key.as_str()),
        target_eid,
        None,
        &client_ip,
        sess.as_ref().is_some_and(|s| s.password_ok),
    ) {
        return resp;
    }
    if let Some(eid) = q.entity_id.as_deref() {
        if !tok.is_empty() {
            let _ = state.bind_session_to_entity(tok, eid).await;
        }
    }
    let host = headers
        .get(header::HOST)
        .and_then(|v| v.to_str().ok())
        .map(host_without_port)
        .filter(|h| !h.is_empty())
        .unwrap_or(&state.config.public_host);
    let tcp_addr = format_host_port(host, state.game_tcp_port);
    tracing::info!(%tcp_addr, %client_ip, "HTTP GET /entry -> 200");
    Json(json!({
        "frontend_addresses": [tcp_addr],
        "radiotower_addresses": [tcp_addr],
        "cluster_mode": "Online",
        "server_mode": state.config.server_mode,
        "cheats_allowed": state.config.server_mode.eq_ignore_ascii_case("sandbox")
    }))
    .into_response()
}

fn resolve_safe_asset_path(base: &Path, raw_rel: &str) -> Option<PathBuf> {
    let rel = raw_rel.trim_start_matches('/');
    if rel.is_empty() || rel.contains('\0') {
        return None;
    }
    let mut clean = PathBuf::new();
    for comp in Path::new(rel).components() {
        match comp {
            Component::Normal(seg) => clean.push(seg),
            _ => return None,
        }
    }
    let direct = base.join(&clean);
    if direct.is_file() {
        return Some(direct);
    }
    let with_json = base.join(format!("{}.json", clean.display()));
    if with_json.is_file() {
        return Some(with_json);
    }
    None
}

fn asset_content_type(path: &Path) -> &'static str {
    match path
        .extension()
        .and_then(|e| e.to_str())
        .map(|s| s.to_ascii_lowercase())
        .as_deref()
    {
        Some("json") => "application/json; charset=utf-8",
        Some("png") => "image/png",
        Some("jpg" | "jpeg") => "image/jpeg",
        Some("wav") => "audio/wav",
        Some("ogg") => "audio/ogg",
        Some("txt" | "yml" | "yaml") => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    }
}

async fn get_asset_file(
    State(state): State<ServerState>,
    AxumPath(rel): AxumPath<String>,
) -> Response {
    let Some(base) = &state.config.assets_dir else {
        return (StatusCode::NOT_FOUND, "assets_dir not configured").into_response();
    };
    if rel.contains("..") {
        return (StatusCode::BAD_REQUEST, "invalid asset path").into_response();
    }
    let Some(path) = resolve_safe_asset_path(base, &rel) else {
        return (StatusCode::NOT_FOUND, "asset not found").into_response();
    };
    let (Ok(canon_base), Ok(canon_path)) = (
        tokio::fs::canonicalize(base).await,
        tokio::fs::canonicalize(&path).await,
    ) else {
        return (StatusCode::NOT_FOUND, "asset not found").into_response();
    };
    if !canon_path.starts_with(&canon_base) {
        return (StatusCode::FORBIDDEN, "forbidden asset path").into_response();
    }
    let ctype = asset_content_type(&canon_path);
    match tokio::fs::read(&canon_path).await {
        Ok(bytes) => (StatusCode::OK, [(header::CONTENT_TYPE, ctype)], bytes).into_response(),
        Err(_) => (StatusCode::INTERNAL_SERVER_ERROR, "read failed").into_response(),
    }
}

fn parse_chunk_xy(s: &str) -> Option<(i32, i32)> {
    let last = s.rsplit('/').next().unwrap_or(s);
    let (xs, ys) = last.split_once(',')?;
    let x = xs.trim().parse::<i32>().ok()?;
    let y = ys.trim().parse::<i32>().ok()?;
    if x < 0 || y < 0 {
        return None;
    }
    Some((x, y))
}

async fn get_terrain_route(
    State(state): State<ServerState>,
    AxumPath(raw): AxumPath<String>,
) -> Response {
    let clean = raw.trim_start_matches('/');
    let (region_id, tail) = match clean.split_once('/') {
        Some((r, t)) => (r, t),
        None => (clean, ""),
    };
    if region_id.is_empty()
        || region_id.len() > 64
        || !region_id
            .chars()
            .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-')
    {
        return (StatusCode::BAD_REQUEST, "invalid terrain id").into_response();
    }
    let resolved_id = crate::state::terrain_id_for_region(region_id);
    let is_known = crate::state::KNOWN_REGION_TEMPLATES
        .iter()
        .any(|m| m.terrain_id.eq_ignore_ascii_case(&resolved_id))
        || state
            .terrains
            .list_available_terrains()
            .iter()
            .any(|t| t.eq_ignore_ascii_case(&resolved_id));
    if !is_known {
        return (StatusCode::NOT_FOUND, "unknown terrain").into_response();
    }
    let terrain = state.terrains.get_or_load(region_id).await;
    if tail.is_empty() {
        return (
            StatusCode::OK,
            [(header::CONTENT_TYPE, "application/json; charset=utf-8")],
            terrain.info_json.clone(),
        )
            .into_response();
    }
    if tail.eq_ignore_ascii_case("whole_biomes") {
        return (
            StatusCode::OK,
            [(header::CONTENT_TYPE, "application/octet-stream")],
            terrain.biomes.clone(),
        )
            .into_response();
    }
    let Some((cx, cy)) = parse_chunk_xy(tail) else {
        return (StatusCode::BAD_REQUEST, "invalid chunk coordinate").into_response();
    };
    if (cx as usize) >= terrain.num_chunks_x() || (cy as usize) >= terrain.num_chunks_y() {
        return (StatusCode::NOT_FOUND, "chunk out of bounds").into_response();
    }
    let bytes = if tail.starts_with("ocean/") {
        terrain.chunk_ocean(cx, cy)
    } else if tail.starts_with("rivers/") {
        terrain.chunk_rivers(cx, cy)
    } else {
        terrain.chunk_combined(cx, cy)
    };
    (
        StatusCode::OK,
        [(header::CONTENT_TYPE, "application/octet-stream")],
        bytes,
    )
        .into_response()
}

// =============================================================================
// Community Server Directory (Source of Truth) & Admin Moderation API + Web UI
// =============================================================================

#[derive(Deserialize)]
struct DirectoryHeartbeatReq {
    server_id: Option<String>,
    name: String,
    gateway: String,
    region: Option<String>,
    online_players: Option<usize>,
    max_players: Option<usize>,
    password_required: Option<bool>,
    pvp_enabled: Option<bool>,
    version: Option<String>,
    nonce: Option<String>,
    ts: Option<i64>,
    exe_sha256: Option<String>,
    assets_digest: Option<String>,
    attestation_sig: Option<String>,
}

#[derive(Deserialize)]
struct AttestationQuery {
    nonce: Option<String>,
}

async fn get_api_attestation(
    State(state): State<ServerState>,
    Query(q): Query<AttestationQuery>,
) -> Json<Value> {
    let nonce = q.nonce.unwrap_or_default();
    let proof = if !nonce.trim().is_empty() {
        state.effective_attestation_key().map(|key| {
            crate::attestation::sign_probe_response(
                key,
                &nonce,
                &state.self_exe_sha256,
                &state.assets_digest,
                "3.3.6",
            )
        })
    } else {
        None
    };
    Json(json!({
        "ok": true,
        "version": "3.3.6",
        "release_id": state.attestation_release_id,
        "git_commit": crate::attestation::COMPILED_GIT_COMMIT.unwrap_or("unknown"),
        "exe_sha256": state.self_exe_sha256,
        "assets_digest": state.assets_digest,
        "nonce": nonce,
        "proof": proof,
    }))
}

#[derive(Deserialize)]
struct DirectoryChallengeQuery {
    server_id: Option<String>,
    gateway: String,
}

async fn get_directory_challenge(
    State(state): State<ServerState>,
    Query(q): Query<DirectoryChallengeQuery>,
) -> Response {
    let gw = q.gateway.trim().trim_end_matches('/').to_string();
    if (!gw.starts_with("http://") && !gw.starts_with("https://")) || gw.len() > 200 {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "invalid_gateway" })),
        )
            .into_response();
    }
    let sid = q
        .server_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .unwrap_or(&gw)
        .to_string();
    let now = ServerState::now_unix_secs();
    let nonce = state.ca_challenges.issue(&sid, &gw, now);
    Json(json!({
        "ok": true,
        "nonce": nonce,
        "server_id": sid,
        "gateway": gw,
        "expires_in": 90
    }))
    .into_response()
}

async fn get_directory_servers(
    State(state): State<ServerState>,
    headers: HeaderMap,
) -> Json<Value> {
    let base = root_url(&headers, &state);
    let now = ServerState::now_unix_secs();
    let servers = state.list_live_directory_servers(&base, now).await;
    Json(json!({
        "ok": true,
        "count": servers.len(),
        "servers": servers
    }))
}

async fn post_directory_heartbeat(
    State(state): State<ServerState>,
    headers: HeaderMap,
    Json(req): Json<DirectoryHeartbeatReq>,
) -> Response {
    if let Some(expected) = state
        .config
        .directory_secret
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        let provided = headers
            .get("x-directory-secret")
            .and_then(|v| v.to_str().ok())
            .map(str::trim)
            .unwrap_or("");
        if !ct_eq(provided, expected) {
            return (
                StatusCode::UNAUTHORIZED,
                Json(json!({ "error": "invalid_directory_secret" })),
            )
                .into_response();
        }
    }

    let gw = req.gateway.trim().trim_end_matches('/').to_string();
    let name = req.name.trim().to_string();
    if name.is_empty()
        || name.len() > 80
        || (!gw.starts_with("http://") && !gw.starts_with("https://"))
        || gw.len() > 200
    {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "invalid_server_entry" })),
        )
            .into_response();
    }
    let now = ServerState::now_unix_secs();
    let sid = req
        .server_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(ToString::to_string)
        .unwrap_or_else(|| gw.clone());
    let version = req
        .version
        .clone()
        .unwrap_or_else(|| "3.3.6".to_string());

    // CA binary & asset attestation enforcement (when CA allowlist or require_attestation is active)
    let allowlist_opt = state.ca_allowlist.read().await.clone();
    let enforce_ca = state.config.ca_require_attestation
        || allowlist_opt.as_ref().is_some_and(|a| a.require_attestation || !a.releases.is_empty());
    let mut verified_flag: Option<bool> = None;
    if enforce_ca {
        let Some(ref allowlist) = allowlist_opt else {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "ca_allowlist_not_configured" })),
            )
                .into_response();
        };
        let Some(nonce) = req.nonce.as_deref().map(str::trim).filter(|s| !s.is_empty()) else {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "missing_challenge_nonce" })),
            )
                .into_response();
        };
        let Some(ts) = req.ts else {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "missing_timestamp" })),
            )
                .into_response();
        };
        if (now as i64 - ts).abs() > 120 {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "timestamp_out_of_window" })),
            )
                .into_response();
        }
        if !state.ca_challenges.consume_and_verify(nonce, &sid, &gw, now) {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "invalid_or_expired_nonce" })),
            )
                .into_response();
        }
        let exe_hash = req.exe_sha256.as_deref().unwrap_or("");
        let assets_hash = req.assets_digest.as_deref().unwrap_or("");
        let sig = req.attestation_sig.as_deref().unwrap_or("");
        let Some(rel) = allowlist.find_matching_release(exe_hash, assets_hash) else {
            tracing::warn!(%sid, %gw, %exe_hash, "CA rejected unregistered/modified server binary");
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "binary_or_assets_not_in_allowlist" })),
            )
                .into_response();
        };
        let expected_sig = crate::attestation::sign_attestation_claim(
            &rel.hmac_key,
            nonce,
            &sid,
            &gw,
            exe_hash,
            assets_hash,
            &version,
            ts,
        );
        if !crate::attestation::ct_eq_hex(sig, &expected_sig) {
            tracing::warn!(%sid, %gw, release = %rel.release_id, "CA rejected invalid attestation signature");
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "invalid_attestation_signature" })),
            )
                .into_response();
        }

        // Active reverse probe back to candidate gateway to prove live identity
        let probe_nonce = uuid::Uuid::new_v4().simple().to_string();
        let probe_url = format!("{gw}/api/attestation?nonce={probe_nonce}");
        let Some(probe_body) = crate::fetch_http_get_url(&probe_url).await else {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "reverse_probe_unreachable" })),
            )
                .into_response();
        };
        let Ok(probe_json) = serde_json::from_str::<Value>(&probe_body) else {
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "reverse_probe_invalid_json" })),
            )
                .into_response();
        };
        let p_exe = probe_json["exe_sha256"].as_str().unwrap_or("");
        let p_assets = probe_json["assets_digest"].as_str().unwrap_or("");
        let p_proof = probe_json["proof"].as_str().unwrap_or("");
        let p_ver = probe_json["version"].as_str().unwrap_or("3.3.6");
        let expected_probe = crate::attestation::sign_probe_response(
            &rel.hmac_key,
            &probe_nonce,
            p_exe,
            p_assets,
            p_ver,
        );
        if !crate::attestation::ct_eq_hex(p_exe, exe_hash)
            || !crate::attestation::ct_eq_hex(p_assets, assets_hash)
            || !crate::attestation::ct_eq_hex(p_proof, &expected_probe)
        {
            tracing::warn!(%sid, %gw, "CA reverse attestation probe mismatch");
            return (
                StatusCode::FORBIDDEN,
                Json(json!({ "error": "untrusted_server_binary", "reason": "reverse_probe_mismatch" })),
            )
                .into_response();
        }
        verified_flag = Some(true);
    }

    {
        let mut dir = state.directory_servers.write().await;
        dir.retain(|_, v| now - v.last_seen_at <= 180.0);
        if !dir.contains_key(&sid) && dir.len() >= 256 {
            return (
                StatusCode::TOO_MANY_REQUESTS,
                Json(json!({ "error": "directory_full" })),
            )
                .into_response();
        }
    }

    let entry = DirectoryServerEntry {
        server_id: sid.clone(),
        name,
        gateway: gw,
        region: req.region.unwrap_or_else(|| "Global".to_string()),
        online_players: req.online_players.unwrap_or(0),
        max_players: req.max_players.unwrap_or(100),
        password_required: req.password_required.unwrap_or(false),
        pvp_enabled: req.pvp_enabled.unwrap_or(false),
        verified: verified_flag,
        version,
        last_seen_at: now,
    };
    state.upsert_directory_server(entry).await;
    Json(json!({ "ok": true, "server_id": sid, "verified": verified_flag == Some(true) })).into_response()
}

#[derive(Deserialize)]
struct DirectoryDeleteReq {
    server_id: String,
}

async fn delete_directory_heartbeat(
    State(state): State<ServerState>,
    headers: HeaderMap,
    Json(req): Json<DirectoryDeleteReq>,
) -> Response {
    if let Some(expected) = state
        .config
        .directory_secret
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        let provided = headers
            .get("x-directory-secret")
            .and_then(|v| v.to_str().ok())
            .map(str::trim)
            .unwrap_or("");
        if !ct_eq(provided, expected) {
            return (
                StatusCode::UNAUTHORIZED,
                Json(json!({ "error": "invalid_directory_secret" })),
            )
                .into_response();
        }
    }
    let removed = state.remove_directory_server(&req.server_id).await;
    Json(json!({ "ok": true, "removed": removed })).into_response()
}

async fn get_directory_html(
    State(state): State<ServerState>,
    headers: HeaderMap,
) -> Html<String> {
    let base = root_url(&headers, &state);
    let now = ServerState::now_unix_secs();
    let servers = state.list_live_directory_servers(&base, now).await;
    let mut rows = String::new();
    for s in servers {
        let lock = if s.password_required { "🔒 Password" } else { "🌐 Open" };
        let trust = if s.verified == Some(true) || s.server_id == "self" {
            "🛡️ Trusted"
        } else {
            "Unverified"
        };
        rows.push_str(&format!(
            "<tr><td><strong>{}</strong> <small>({})</small></td><td>{}</td><td>{}/{}</td><td>{}</td><td><code>{}</code></td></tr>",
            html_escape(&s.name),
            trust,
            html_escape(&s.region),
            s.online_players,
            s.max_players,
            lock,
            html_escape(&s.gateway),
        ));
    }
    Html(format!(
        r#"<!doctype html><html><head><meta charset="utf-8"><title>Durango-CE Community Servers</title>
<style>body{{font-family:system-ui,sans-serif;background:#0f1419;color:#e6edf3;margin:2rem auto;max-width:960px;padding:0 1rem}}
h1{{margin-bottom:.25rem}}p.sub{{color:#8b949e;margin-top:0}}table{{width:100%;border-collapse:collapse;background:#161b22;border:1px solid #30363d;border-radius:8px;overflow:hidden}}
th,td{{padding:.75rem 1rem;border-bottom:1px solid #30363d;text-align:left}}th{{background:#1f2630;color:#9eb3c8}}code{{background:#0d1117;padding:.2rem .4rem;border-radius:4px}}</style></head>
<body><h1>Durango-CE Active Community Servers</h1>
<p class="sub">Live source-of-truth server directory (<code>/servers.json</code>) &middot; Auto-prunes offline servers after 90s</p>
<table><thead><tr><th>Server</th><th>Region</th><th>Players</th><th>Access</th><th>Gateway</th></tr></thead><tbody>{}</tbody></table></body></html>"#,
        rows
    ))
}

fn html_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
}

async fn admin_list_players(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let now = ServerState::now_unix_secs();
    let live_snap: Vec<crate::state::LiveClient> = state
        .live_clients
        .read()
        .await
        .values()
        .cloned()
        .collect();
    let online_ids: Vec<String> = live_snap.iter().map(|c| c.entity_id.clone()).collect();
    let online_t_stones = state.db.get_t_stone_batch(&online_ids).unwrap_or_default();
    let online: Vec<Value> = live_snap
        .into_iter()
        .map(|c| {
            let t_stone = online_t_stones
                .get(&c.entity_id)
                .copied()
                .unwrap_or(crate::state::DEFAULT_STARTING_T_STONE);
            json!({
                "entity_id": c.entity_id,
                "owner_key": c.owner_key,
                "account_id": crate::state::format_account_tag(&c.owner_key),
                "account_tag": crate::state::format_account_tag(&c.owner_key),
                "name": c.name,
                "level": c.level,
                "region_id": c.region_id,
                "region_code": crate::state::format_region_code(&c.region_id),
                "region_label": crate::state::region_display_name(&c.region_id),
                "ip": c.peer_ip,
                "pos_x": c.pos_x,
                "pos_y": c.pos_y,
                "life": c.life,
                "energy": c.energy,
                "fatigue": c.fatigue,
                "is_resting": c.is_resting,
                "t_stone": t_stone,
            })
        })
        .collect();
    let recent_rows = state.db.list_recent_characters(100).unwrap_or_default();
    let recent_ids: Vec<String> = recent_rows.iter().map(|r| r.entity_id.clone()).collect();
    let recent_t_stones = state.db.get_t_stone_batch(&recent_ids).unwrap_or_default();
    let recent: Vec<Value> = recent_rows
        .into_iter()
        .map(|r| {
            let t_stone = recent_t_stones
                .get(&r.entity_id)
                .copied()
                .unwrap_or(crate::state::DEFAULT_STARTING_T_STONE);
            json!({
                "entity_id": r.entity_id,
                "owner_key": r.owner_key,
                "account_id": crate::state::format_account_tag(&r.owner_key),
                "account_tag": crate::state::format_account_tag(&r.owner_key),
                "name": r.name,
                "level": r.level,
                "region_id": r.region_id,
                "region_code": crate::state::format_region_code(&r.region_id),
                "region_label": crate::state::region_display_name(&r.region_id),
                "last_ip": r.last_ip,
                "disconnected_at": r.disconnected_at,
                "t_stone": t_stone,
            })
        })
        .collect();
    let uptime_secs = (now - state.started_at).max(0.0) as u64;
    Json(json!({
        "ok": true,
        "server_time": now,
        "uptime_secs": uptime_secs,
        "online": online,
        "recent": recent
    }))
    .into_response()
}

#[derive(Deserialize)]
struct AdminLogsQuery {
    category: Option<String>,
    q: Option<String>,
    limit: Option<usize>,
}

async fn admin_list_logs(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Query(q): Query<AdminLogsQuery>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let rows = state
        .db
        .list_audit_logs(
            q.category.as_deref(),
            q.q.as_deref(),
            q.limit.unwrap_or(150),
        )
        .unwrap_or_default();
    let logs: Vec<Value> = rows
        .into_iter()
        .map(|l| {
            let region_label = if l.region_id.is_empty() {
                String::new()
            } else {
                crate::state::region_display_name(&l.region_id)
            };
            let region_code = crate::state::format_region_code(&l.region_id);
            json!({
                "id": l.id,
                "ts": l.ts,
                "category": l.category,
                "actor": l.actor,
                "actor_ip": l.actor_ip,
                "region_id": l.region_id,
                "region_code": region_code,
                "region_label": region_label,
                "summary": l.summary
            })
        })
        .collect();
    Json(json!({ "ok": true, "logs": logs })).into_response()
}

async fn admin_list_bans(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let now = ServerState::now_unix_secs();
    let bans = state.db.list_active_bans(now).unwrap_or_default();
    Json(json!({ "ok": true, "bans": bans })).into_response()
}

#[derive(Deserialize)]
struct AdminAddBanReq {
    ban_type: String,
    target: String,
    reason: Option<String>,
    duration_hours: Option<f64>,
    include_ip: Option<bool>,
}

async fn admin_add_ban(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Json(req): Json<AdminAddBanReq>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let now = ServerState::now_unix_secs();
    let bt = req.ban_type.trim().to_ascii_lowercase();
    let target = req.target.trim();
    if target.is_empty() {
        return (
            StatusCode::BAD_REQUEST,
            Json(json!({ "error": "empty_target" })),
        )
            .into_response();
    }
    let reason = req.reason.as_deref().unwrap_or("").trim();
    let expires_at = req
        .duration_hours
        .filter(|&h| h > 0.0)
        .map(|h| now + h * 3600.0);

    let mut created_ids = Vec::new();
    let mut kicked = 0usize;

    match bt.as_str() {
        "character" | "name" => {
            if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(target) {
                if let Ok(id) = state
                    .db
                    .add_ban("account", &ch.owner_key, reason, "admin", now, expires_at)
                {
                    created_ids.push(id);
                }
                if let Ok(id) = state
                    .db
                    .add_ban("entity", &ch.entity_id, reason, "admin", now, expires_at)
                {
                    created_ids.push(id);
                }
                if req.include_ip.unwrap_or(false) && !ch.last_ip.is_empty() {
                    if let Ok(id) = state
                        .db
                        .add_ban("ip", &ch.last_ip, reason, "admin", now, expires_at)
                    {
                        created_ids.push(id);
                    }
                    kicked += state.kick_by_ip(&ch.last_ip).await;
                }
                kicked += state.kick_by_owner(&ch.owner_key).await;
            } else if let Ok(id) = state
                .db
                .add_ban("name", target, reason, "admin", now, expires_at)
            {
                created_ids.push(id);
            }
        }
        "account" => {
            let canon_target = crate::state::canonical_account_id(target)
                .unwrap_or_else(|| target.to_string());
            if let Ok(id) = state
                .db
                .add_ban("account", &canon_target, reason, "admin", now, expires_at)
            {
                created_ids.push(id);
            }
            kicked += state.kick_by_owner(&canon_target).await;
        }
        "entity" => {
            if let Ok(id) = state
                .db
                .add_ban("entity", target, reason, "admin", now, expires_at)
            {
                created_ids.push(id);
            }
            if state.kick_by_entity(target).await {
                kicked += 1;
            }
        }
        "ip" | "cidr" => {
            if let Ok(id) = state
                .db
                .add_ban("ip", target, reason, "admin", now, expires_at)
            {
                created_ids.push(id);
            }
            kicked += state.kick_by_ip(target).await;
        }
        "mute" => {
            let resolved = state
                .db
                .resolve_character_by_name_or_id(target)
                .ok()
                .flatten()
                .map(|c| c.owner_key)
                .unwrap_or_else(|| {
                    crate::state::canonical_account_id(target).unwrap_or_else(|| target.to_string())
                });
            if let Ok(id) = state
                .db
                .add_ban("mute", &resolved, reason, "admin", now, expires_at)
            {
                created_ids.push(id);
            }
        }
        _ => {
            return (
                StatusCode::BAD_REQUEST,
                Json(json!({ "error": "invalid_ban_type (use account|character|entity|ip|mute)" })),
            )
                .into_response();
        }
    }

    tracing::info!(ban_type = %bt, %target, kicked, "Admin added ban rule");
    state.db.log_audit(
        now,
        "MODERATION",
        "admin",
        "",
        "",
        &format!("Added {bt} rule for '{target}' (kicked {kicked}, reason: {reason})"),
    );
    Json(json!({
        "ok": true,
        "created_ids": created_ids,
        "kicked": kicked
    }))
    .into_response()
}

async fn admin_delete_ban(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    AxumPath(id): AxumPath<i64>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let removed = state.db.remove_ban_by_id(id).unwrap_or(false);
    if removed {
        state.db.log_audit(
            ServerState::now_unix_secs(),
            "MODERATION",
            "admin",
            "",
            "",
            &format!("Removed ban rule #{id}"),
        );
    }
    Json(json!({ "ok": true, "removed": removed })).into_response()
}

#[derive(Deserialize)]
struct AdminKickReq {
    target: String,
}

async fn admin_kick_player(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Json(req): Json<AdminKickReq>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let target = req.target.trim();
    let mut kicked = 0usize;
    if state.kick_by_entity(target).await {
        kicked += 1;
    } else if let Ok(Some(ch)) = state.db.resolve_character_by_name_or_id(target) {
        kicked += state.kick_by_owner(&ch.owner_key).await;
    } else {
        kicked += state.kick_by_owner(target).await;
    }
    state.db.log_audit(
        ServerState::now_unix_secs(),
        "MODERATION",
        "admin",
        "",
        "",
        &format!("Kicked '{target}' ({kicked} session(s) disconnected)"),
    );
    Json(json!({ "ok": true, "kicked": kicked })).into_response()
}

async fn admin_get_config(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    Json(json!({
        "ok": true,
        "server_name": state.effective_server_name(),
        "region": state.config.region_tag,
        "max_players": state.config.max_players,
        "password_required": state.effective_join_password().is_some(),
        "notice": state.effective_notice()
    }))
    .into_response()
}

#[derive(Deserialize)]
struct AdminUpdateConfigReq {
    server_name: Option<String>,
    join_password: Option<String>,
    notice: Option<String>,
}

async fn admin_update_config(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Json(req): Json<AdminUpdateConfigReq>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let now = ServerState::now_unix_secs();
    let mut changes = Vec::new();
    if let Some(name) = req.server_name {
        let _ = state.db.set_setting("server_name", name.trim(), now);
        changes.push(format!("name='{}'", name.trim()));
    }
    if let Some(pw) = req.join_password {
        let trimmed = pw.trim();
        let _ = state.db.set_setting("join_password", trimmed, now);
        if !trimmed.is_empty() {
            let mut sessions = state.sessions.write().await;
            for s in sessions.values_mut() {
                s.password_ok = false;
            }
            changes.push("join_password=ENABLED".into());
        } else {
            changes.push("join_password=OPEN".into());
        }
    }
    if let Some(notice) = req.notice {
        let _ = state.db.set_setting("notice", &notice, now);
        changes.push("notice updated".into());
    }
    if !changes.is_empty() {
        state.db.log_audit(
            now,
            "SERVER",
            "admin",
            "",
            "",
            &format!("Updated server settings: {}", changes.join(", ")),
        );
    }
    Json(json!({
        "ok": true,
        "server_name": state.effective_server_name(),
        "password_required": state.effective_join_password().is_some(),
        "notice": state.effective_notice()
    }))
    .into_response()
}

async fn admin_remove_directory_server(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    AxumPath(server_id): AxumPath<String>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let removed = state.remove_directory_server(&server_id).await;
    Json(json!({ "ok": true, "removed": removed })).into_response()
}

#[derive(Deserialize)]
struct AdminSnapshotQuery {
    category: Option<String>,
    q: Option<String>,
    limit: Option<usize>,
    range_secs: Option<u64>,
}

async fn admin_snapshot(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Query(q): Query<AdminSnapshotQuery>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    state.sample_metrics().await;
    let now = ServerState::now_unix_secs();
    let base = root_url(&headers, &state);
    let range_secs = q.range_secs.unwrap_or(300).clamp(60, 86_400) as f64;

    let live_snap: Vec<crate::state::LiveClient> = state
        .live_clients
        .read()
        .await
        .values()
        .cloned()
        .collect();
    let online_ids: Vec<String> = live_snap.iter().map(|c| c.entity_id.clone()).collect();
    let online_t_stones = state.db.get_t_stone_batch(&online_ids).unwrap_or_default();
    let online: Vec<Value> = live_snap
        .into_iter()
        .map(|c| {
            let t_stone = online_t_stones
                .get(&c.entity_id)
                .copied()
                .unwrap_or(crate::state::DEFAULT_STARTING_T_STONE);
            json!({
                "entity_id": c.entity_id,
                "owner_key": c.owner_key,
                "account_id": crate::state::format_account_tag(&c.owner_key),
                "account_tag": crate::state::format_account_tag(&c.owner_key),
                "name": c.name,
                "level": c.level,
                "region_id": c.region_id,
                "region_code": crate::state::format_region_code(&c.region_id),
                "region_label": crate::state::region_display_name(&c.region_id),
                "ip": c.peer_ip,
                "tile_x": (c.pos_x / 200.0).round() as i32,
                "tile_y": (c.pos_y / 200.0).round() as i32,
                "life": c.life,
                "energy": c.energy,
                "fatigue": c.fatigue,
                "is_resting": c.is_resting,
                "t_stone": t_stone,
            })
        })
        .collect();

    let recent_rows = state.db.list_recent_characters(100).unwrap_or_default();
    let recent_ids: Vec<String> = recent_rows.iter().map(|r| r.entity_id.clone()).collect();
    let recent_t_stones = state.db.get_t_stone_batch(&recent_ids).unwrap_or_default();
    let recent: Vec<Value> = recent_rows
        .into_iter()
        .map(|r| {
            let t_stone = recent_t_stones
                .get(&r.entity_id)
                .copied()
                .unwrap_or(crate::state::DEFAULT_STARTING_T_STONE);
            json!({
                "entity_id": r.entity_id,
                "owner_key": r.owner_key,
                "account_id": crate::state::format_account_tag(&r.owner_key),
                "account_tag": crate::state::format_account_tag(&r.owner_key),
                "name": r.name,
                "level": r.level,
                "region_id": r.region_id,
                "region_code": crate::state::format_region_code(&r.region_id),
                "region_label": crate::state::region_display_name(&r.region_id),
                "last_ip": r.last_ip,
                "disconnected_at": r.disconnected_at,
                "t_stone": t_stone,
            })
        })
        .collect();

    let bans = state.db.list_active_bans(now).unwrap_or_default();
    let directory = state.list_live_directory_servers(&base, now).await;
    let logs: Vec<Value> = state
        .db
        .list_audit_logs(
            q.category.as_deref(),
            q.q.as_deref(),
            q.limit.unwrap_or(150),
        )
        .unwrap_or_default()
        .into_iter()
        .map(|l| {
            let region_label = if l.region_id.is_empty() {
                String::new()
            } else {
                crate::state::region_display_name(&l.region_id)
            };
            json!({
                "id": l.id,
                "ts": l.ts,
                "category": l.category,
                "actor": l.actor,
                "actor_ip": l.actor_ip,
                "region_id": l.region_id,
                "region_code": crate::state::format_region_code(&l.region_id),
                "region_label": region_label,
                "summary": l.summary
            })
        })
        .collect();

    let metrics: Vec<crate::state::MetricSample> = state
        .metrics_history
        .read()
        .await
        .iter()
        .filter(|m| now - m.ts <= range_secs)
        .cloned()
        .collect();
    let sessions_count = state.sessions.read().await.len();
    let uptime_secs = (now - state.started_at).max(0.0) as u64;
    let db_disk = state.db.clone();
    let (log_archives, log_disk_bytes) = tokio::task::spawn_blocking(move || db_disk.audit_disk_stats())
        .await
        .unwrap_or((0, 0));

    Json(json!({
        "ok": true,
        "server_time": now,
        "uptime_secs": uptime_secs,
        "sessions_count": sessions_count,
        "log_archives": log_archives,
        "log_disk_bytes": log_disk_bytes,
        "config": {
            "server_name": state.effective_server_name(),
            "region": state.config.region_tag,
            "max_players": state.config.max_players,
            "password_required": state.effective_join_password().is_some(),
            "notice": state.effective_notice(),
        },
        "online": online,
        "recent": recent,
        "bans": bans,
        "directory": directory,
        "logs": logs,
        "metrics": metrics,
    }))
    .into_response()
}

fn prom_escape(s: &str) -> String {
    s.replace('\\', "\\\\")
        .replace('"', "\\\"")
        .replace('\n', " ")
}

async fn get_prometheus_metrics(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let now = ServerState::now_unix_secs();
    let clients_snap: Vec<crate::state::LiveClient> = state
        .live_clients
        .read()
        .await
        .values()
        .cloned()
        .collect();
    let online = clients_snap.len();
    let resting = clients_snap.iter().filter(|c| c.is_resting).count();
    let sessions = state.sessions.read().await.len();
    let known = state.db.count_characters().unwrap_or(0);
    let bans = state.db.count_active_bans(now).unwrap_or(0);
    let recent_logs = state.db.list_audit_logs(None, None, 500).unwrap_or_default();
    let events_1m = recent_logs.iter().filter(|l| now - l.ts <= 60.0).count();
    let uptime = (now - state.started_at).max(0.0);
    let db_disk = state.db.clone();
    let (archives, log_bytes) = tokio::task::spawn_blocking(move || db_disk.audit_disk_stats())
        .await
        .unwrap_or((0, 0));
    let pw_req = usize::from(state.effective_join_password().is_some());

    let mut body = format!(
        "# HELP durango_players_online Currently connected players\n\
         # TYPE durango_players_online gauge\n\
         durango_players_online {online}\n\
         # HELP durango_players_resting Currently resting players\n\
         # TYPE durango_players_resting gauge\n\
         durango_players_resting {resting}\n\
         # HELP durango_sessions_active Active authenticated sessions\n\
         # TYPE durango_sessions_active gauge\n\
         durango_sessions_active {sessions}\n\
         # HELP durango_characters_known Total persisted characters\n\
         # TYPE durango_characters_known gauge\n\
         durango_characters_known {known}\n\
         # HELP durango_bans_active Active ban and mute rules\n\
         # TYPE durango_bans_active gauge\n\
         durango_bans_active {bans}\n\
         # HELP durango_password_required Whether join password gate is enabled (1=yes, 0=open)\n\
         # TYPE durango_password_required gauge\n\
         durango_password_required {pw_req}\n\
         # HELP durango_audit_events_1m Audit events recorded in the last 60s\n\
         # TYPE durango_audit_events_1m gauge\n\
         durango_audit_events_1m {events_1m}\n\
         # HELP durango_audit_log_bytes Total audit log bytes on disk (active + gzipped archives)\n\
         # TYPE durango_audit_log_bytes gauge\n\
         durango_audit_log_bytes {log_bytes}\n\
         # HELP durango_audit_archives_count Rotated gzipped audit log archives\n\
         # TYPE durango_audit_archives_count gauge\n\
         durango_audit_archives_count {archives}\n\
         # HELP durango_uptime_seconds Server process uptime in seconds\n\
         # TYPE durango_uptime_seconds counter\n\
         durango_uptime_seconds {uptime:.1}\n\
         # HELP durango_audit_events_window Recent audit events by category\n\
         # TYPE durango_audit_events_window gauge\n"
    );

    for cat in [
        "SERVER",
        "AUTH",
        "WORLD",
        "GATHER",
        "CRAFT",
        "BUILD",
        "DOMAIN",
        "COMBAT",
        "SURVIVAL",
        "EQUIP",
        "ITEM",
        "SKILL",
        "SOCIAL",
        "MARKET",
        "CHAT",
        "MODERATION",
    ] {
        let cnt = recent_logs
            .iter()
            .filter(|l| l.category.eq_ignore_ascii_case(cat))
            .count();
        body.push_str(&format!(
            "durango_audit_events_window{{category=\"{cat}\"}} {cnt}\n"
        ));
    }

    let client_ids: Vec<String> = clients_snap.iter().map(|c| c.entity_id.clone()).collect();
    let t_stones = state.db.get_t_stone_batch(&client_ids).unwrap_or_default();
    let player_rows: Vec<(String, i32, f32, f32, f32, i64)> = clients_snap
        .iter()
        .map(|c| {
            let name = prom_escape(&c.name);
            let acct = prom_escape(&c.owner_key);
            let zone = prom_escape(&c.region_id);
            let ip = prom_escape(&c.peer_ip);
            let t_stone = t_stones
                .get(&c.entity_id)
                .copied()
                .unwrap_or(crate::state::DEFAULT_STARTING_T_STONE);
            let labels = format!("name=\"{name}\",account=\"{acct}\",zone=\"{zone}\",ip=\"{ip}\"");
            (labels, c.level, c.life, c.fatigue, c.energy, t_stone)
        })
        .collect();

    body.push_str("# HELP durango_player_level Online player level\n# TYPE durango_player_level gauge\n");
    for (labels, level, _, _, _, _) in &player_rows {
        body.push_str(&format!("durango_player_level{{{labels}}} {level}\n"));
    }
    body.push_str("# HELP durango_player_life Online player HP\n# TYPE durango_player_life gauge\n");
    for (labels, _, life, _, _, _) in &player_rows {
        body.push_str(&format!("durango_player_life{{{labels}}} {life:.1}\n"));
    }
    body.push_str("# HELP durango_player_fatigue Online player Fatigue\n# TYPE durango_player_fatigue gauge\n");
    for (labels, _, _, fatigue, _, _) in &player_rows {
        body.push_str(&format!("durango_player_fatigue{{{labels}}} {fatigue:.1}\n"));
    }
    body.push_str("# HELP durango_player_energy Online player Energy\n# TYPE durango_player_energy gauge\n");
    for (labels, _, _, _, energy, _) in &player_rows {
        body.push_str(&format!("durango_player_energy{{{labels}}} {energy:.1}\n"));
    }
    body.push_str("# HELP durango_player_t_stone Online player T-Stone balance\n# TYPE durango_player_t_stone gauge\n");
    for (labels, _, _, _, _, t_stone) in &player_rows {
        body.push_str(&format!("durango_player_t_stone{{{labels}}} {t_stone}\n"));
    }

    (
        StatusCode::OK,
        [(header::CONTENT_TYPE, "text/plain; version=0.0.4; charset=utf-8")],
        body,
    )
        .into_response()
}

async fn admin_export_logs(
    State(state): State<ServerState>,
    connect_info: Option<ConnectInfo<SocketAddr>>,
    headers: HeaderMap,
    Query(q): Query<AdminLogsQuery>,
) -> Response {
    if let Err(resp) = verify_admin_request(&state, connect_info, &headers) {
        return resp;
    }
    let rows = state
        .db
        .list_audit_logs(q.category.as_deref(), q.q.as_deref(), 500)
        .unwrap_or_default();
    let mut out = String::new();
    for l in rows {
        if let Ok(line) = serde_json::to_string(&json!({
            "id": l.id,
            "ts": l.ts,
            "category": l.category,
            "actor": l.actor,
            "actor_ip": l.actor_ip,
            "region_id": l.region_id,
            "region_code": crate::state::format_region_code(&l.region_id),
            "summary": l.summary
        })) {
            out.push_str(&line);
            out.push('\n');
        }
    }
    (
        StatusCode::OK,
        [
            (header::CONTENT_TYPE, "application/x-ndjson; charset=utf-8"),
            (
                header::CONTENT_DISPOSITION,
                "attachment; filename=\"durango-audit-logs.jsonl\"",
            ),
        ],
        out,
    )
        .into_response()
}

async fn get_admin_html() -> Html<&'static str> {
    Html(r##"<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Durango-CE · Grafana Operations</title>
<style>
:root{
  --canvas:#111217;--panel:#181b1f;--panel-hd:#1f2329;--chrome:#22252b;--hover:#262a32;
  --border:#2c3235;--border-strong:#383f47;--text:#ccccdc;--text-bright:#f0f2f5;--muted:#8e939c;--subtle:#6e737d;
  --blue:#3274d9;--blue-hi:#5794f2;--orange:#ff9830;--green:#73bf69;--yellow:#fade2a;--red:#f2495c;--purple:#b877d9;
  --mono:'JetBrains Mono','Fira Code',ui-monospace,SFMono-Regular,Menlo,monospace;
  --sans:Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
}
*{box-sizing:border-box}
html,body{margin:0;height:100%;background:var(--canvas);color:var(--text);font-family:var(--sans);font-size:12px;line-height:1.4;-webkit-font-smoothing:antialiased}
.shell{display:grid;grid-template-columns:52px 1fr;grid-template-rows:44px 1fr;height:100vh;overflow:hidden}
/* Left Grafana Nav Rail */
.rail{grid-row:1/3;background:var(--panel);border-right:1px solid var(--border);display:flex;flex-direction:column;align-items:center;padding:8px 0;gap:4px;z-index:30}
.logo{width:34px;height:34px;display:flex;align-items:center;justify-content:center;margin-bottom:8px;color:var(--orange);border-radius:2px}
.nav-btn{position:relative;width:40px;height:36px;display:flex;align-items:center;justify-content:center;background:transparent;border:none;color:var(--muted);cursor:pointer;border-radius:2px;transition:background .12s,color .12s}
.nav-btn:hover{background:var(--hover);color:var(--text-bright)}
.nav-btn.active{background:rgba(255,152,48,.12);color:var(--text-bright)}
.nav-btn.active::before{content:'';position:absolute;left:-6px;top:4px;bottom:4px;width:3px;background:var(--orange);border-radius:1px}
.rail-spacer{flex:1}
/* Top Grafana Toolbar */
.topbar{grid-column:2;background:var(--panel);border-bottom:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;padding:0 12px;gap:10px;min-width:0}
.crumbs{display:flex;align-items:center;gap:6px;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.crumbs .sep{color:var(--subtle)}
.crumbs .cur{color:var(--text-bright);font-weight:600}
.live-pill{display:inline-flex;align-items:center;gap:5px;font-size:11px;font-family:var(--mono);padding:2px 6px;border-radius:2px;background:var(--chrome);border:1px solid var(--border);color:var(--muted)}
.live-pill .dot{width:7px;height:7px;border-radius:50%;background:var(--subtle)}
.live-pill.ok{color:var(--green);border-color:rgba(115,191,105,.35);background:rgba(115,191,105,.08)}
.live-pill.ok .dot{background:var(--green);box-shadow:0 0 6px var(--green)}
.live-pill.err{color:var(--red);border-color:rgba(242,73,92,.4);background:rgba(242,73,92,.08)}
.live-pill.err .dot{background:var(--red)}
.controls{display:flex;align-items:center;gap:6px;flex-shrink:0}
.gf-group{display:inline-flex;align-items:stretch}
.gf-group>*{border-radius:0;margin-left:-1px}
.gf-group>*:first-child{border-radius:2px 0 0 2px;margin-left:0}
.gf-group>*:last-child{border-radius:0 2px 2px 0}
input,select,textarea,button{font-family:var(--sans);font-size:12px;background:#111217;color:var(--text);border:1px solid var(--border-strong);border-radius:2px;padding:4px 8px;height:28px;outline:none}
textarea{height:auto;padding:6px 8px}
input:focus,select:focus,textarea:focus{border-color:var(--blue-hi);box-shadow:0 0 0 1px var(--blue-hi)}
button{background:var(--chrome);color:var(--text-bright);cursor:pointer;display:inline-flex;align-items:center;gap:5px;font-weight:500;white-space:nowrap}
button:hover{background:var(--hover);border-color:#4b5563}
button.pri{background:var(--blue);border-color:var(--blue);color:#fff}
button.pri:hover{background:#3d80e6}
button.danger{background:rgba(242,73,92,.14);border-color:rgba(242,73,92,.5);color:#ff7b89}
button.danger:hover{background:var(--red);border-color:var(--red);color:#fff}
button.warn{background:rgba(250,222,42,.12);border-color:rgba(250,222,42,.4);color:var(--yellow)}
button.warn:hover{background:rgba(250,222,42,.24)}
button.xs{height:22px;padding:0 7px;font-size:11px}
.icon-btn{width:28px;padding:0;justify-content:center}
/* Workspace */
.work{grid-column:2;overflow-y:auto;padding:10px;display:flex;flex-direction:column;gap:8px}
.view{display:none;flex-direction:column;gap:8px}.view.active{display:flex}
/* Grafana 12-Col Grid & Panels */
.grid{display:grid;grid-template-columns:repeat(12,minmax(0,1fr));gap:8px}
.c2{grid-column:span 2}.c3{grid-column:span 3}.c4{grid-column:span 4}.c5{grid-column:span 5}.c6{grid-column:span 6}.c7{grid-column:span 7}.c8{grid-column:span 8}.c12{grid-column:span 12}
@media(max-width:1180px){.c2{grid-column:span 4}.c6,.c5,.c7{grid-column:span 12}}
.panel{background:var(--panel);border:1px solid var(--border);border-radius:2px;display:flex;flex-direction:column;min-width:0}
.panel-hd{display:flex;align-items:center;justify-content:space-between;padding:6px 10px;border-bottom:1px solid rgba(204,204,220,.06);gap:8px;min-height:30px}
.panel-title{font-size:12px;font-weight:600;color:var(--text-bright);display:flex;align-items:center;gap:6px;white-space:nowrap}
.panel-sub{font-size:11px;color:var(--muted);font-weight:400;font-family:var(--mono)}
.panel-bd{padding:8px 10px;flex:1;min-width:0}
.panel-bd.flush{padding:0}
/* Stat Panel */
.stat-bd{padding:8px 10px 4px;display:flex;flex-direction:column;justify-content:space-between;min-height:84px;position:relative;overflow:hidden}
.stat-val{font-family:var(--mono);font-size:24px;font-weight:600;color:var(--text-bright);line-height:1.15;z-index:2}
.stat-meta{font-size:11px;color:var(--muted);font-family:var(--mono);margin-top:2px;z-index:2}
.spark{width:100%;height:26px;margin-top:4px;display:block}
.t-green{color:var(--green)}.t-blue{color:var(--blue-hi)}.t-yellow{color:var(--yellow)}.t-red{color:var(--red)}.t-orange{color:var(--orange)}
/* Time Series Chart */
.ts-wrap{position:relative;width:100%;height:190px}
.ts-svg{width:100%;height:100%;display:block}
.ts-legend{display:flex;align-items:center;gap:16px;padding:5px 10px 7px;border-top:1px solid rgba(204,204,220,.06);font-family:var(--mono);font-size:11px;color:var(--muted);flex-wrap:wrap}
.leg-item{display:inline-flex;align-items:center;gap:6px}
.leg-sw{width:10px;height:3px;border-radius:1px;display:inline-block}
.leg-item b{color:var(--text-bright);font-weight:600}
/* Dense Grafana Table */
.tbl-wrap{overflow-x:auto}
table{width:100%;border-collapse:collapse;font-size:12px}
th,td{padding:5px 9px;border-bottom:1px solid rgba(204,204,220,.07);text-align:left;vertical-align:middle;white-space:nowrap}
th{background:var(--panel-hd);color:var(--blue-hi);font-size:11px;font-weight:600;border-bottom:1px solid var(--border);position:sticky;top:0}
tbody tr:hover td{background:rgba(255,255,255,.025)}
.mono{font-family:var(--mono);font-size:11px}
.code-chip{font-family:var(--mono);font-size:11px;background:#111217;border:1px solid var(--border);padding:1px 5px;border-radius:2px;color:#8ab8ff;cursor:pointer;display:inline-block}
.code-chip:hover{border-color:var(--blue-hi);color:#fff}
.sub{display:block;font-size:10.5px;color:var(--muted);margin-top:1px}
.tag{display:inline-block;padding:1px 5px;border-radius:2px;font-family:var(--mono);font-size:10.5px;font-weight:600;border:1px solid transparent}
.tg-ok{background:rgba(115,191,105,.14);color:var(--green);border-color:rgba(115,191,105,.35)}
.tg-off{background:rgba(142,147,156,.12);color:var(--muted);border-color:rgba(142,147,156,.25)}
.tg-info{background:rgba(87,148,242,.14);color:var(--blue-hi);border-color:rgba(87,148,242,.35)}
.tg-warn{background:rgba(255,152,48,.14);color:var(--orange);border-color:rgba(255,152,48,.35)}
.tg-err{background:rgba(242,73,92,.14);color:var(--red);border-color:rgba(242,73,92,.4)}
.tg-purp{background:rgba(184,119,217,.14);color:var(--purple);border-color:rgba(184,119,217,.35)}
/* Loki-style Log Stream */
.loki-bar{display:flex;align-items:center;gap:6px;flex-wrap:wrap;padding:6px 10px;background:var(--panel-hd);border-bottom:1px solid var(--border)}
.cat-btn{height:22px;padding:0 7px;font-size:11px;font-family:var(--mono);background:#111217;border:1px solid var(--border);color:var(--muted)}
.cat-btn.active{background:rgba(50,116,217,.22);border-color:var(--blue-hi);color:#fff}
.loki-list{font-family:var(--mono);font-size:11.5px;max-height:540px;overflow-y:auto;background:#0e1014}
.loki-list.compact{max-height:290px}
.log-row{display:grid;grid-template-columns:76px 86px 115px 1fr;gap:8px;padding:4px 10px 4px 8px;border-bottom:1px solid rgba(204,204,220,.04);border-left:3px solid var(--subtle);align-items:baseline}
.log-row:hover{background:rgba(255,255,255,.03)}
.lv-SERVER{border-left-color:var(--blue-hi)}.lv-WORLD{border-left-color:var(--green)}.lv-BUILD,.lv-DOMAIN{border-left-color:#38bdf8}
.lv-GATHER,.lv-CRAFT{border-left-color:#2dd4bf}
.lv-COMBAT{border-left-color:var(--orange)}.lv-MARKET{border-left-color:var(--yellow)}.lv-CHAT{border-left-color:var(--purple)}
.lv-MODERATION,.lv-AUTH{border-left-color:var(--red)}
.log-ts{color:var(--subtle)}.log-actor{color:#9ec5fe;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.log-msg{color:var(--text);white-space:normal;word-break:break-word}
.log-kv{color:var(--muted);margin-left:8px;font-size:10.5px}
.row{display:flex;align-items:center;gap:6px;flex-wrap:wrap}
.empty{color:var(--subtle);text-align:center;padding:24px;font-family:var(--mono);font-size:11.5px}
.toast{position:fixed;right:14px;bottom:14px;background:var(--chrome);border:1px solid var(--blue-hi);color:var(--text-bright);padding:6px 11px;border-radius:2px;font-family:var(--mono);font-size:11.5px;opacity:0;pointer-events:none;transition:opacity .15s;z-index:99}
.toast.show{opacity:1}
</style></head><body>
<div class="shell">
  <!-- Grafana Left Navigation Rail -->
  <nav class="rail" aria-label="Navigation">
    <div class="logo" title="Durango-CE Operations">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2L2 7l10 5 10-5-10-5z"/><path d="M2 17l10 5 10-5"/><path d="M2 12l10 5 10-5"/></svg>
    </div>
    <button class="nav-btn active" data-view="overview" onclick="setView('overview',this)" title="Dashboards · Server Overview">
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
    </button>
    <button class="nav-btn" data-view="players" onclick="setView('players',this)" title="Players &amp; Sessions">
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
    </button>
    <button class="nav-btn" data-view="logs" onclick="setView('logs',this)" title="Explore · Live Activity &amp; Audit Logs">
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="4 17 10 11 4 5"/><line x1="12" y1="19" x2="20" y2="19"/></svg>
    </button>
    <button class="nav-btn" data-view="moderation" onclick="setView('moderation',this)" title="Moderation · Bans &amp; Mutes">
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
    </button>
    <button class="nav-btn" data-view="settings" onclick="setView('settings',this)" title="Administration · Server &amp; Directory">
      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.26.6.85 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
    </button>
    <div class="rail-spacer"></div>
    <button class="nav-btn" onclick="toggleTokenBox()" title="Admin Token">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
    </button>
  </nav>

  <!-- Grafana Top Control Bar -->
  <header class="topbar">
    <div class="crumbs">
      <span style="color:var(--muted)">Durango-CE</span>
      <span class="sep">/</span>
      <span id="srvName" style="color:var(--muted)">Operations</span>
      <span class="sep">/</span>
      <span class="cur" id="crumbView">Server Overview</span>
      <span id="statusPill" class="live-pill"><span class="dot"></span><span id="statusTxt">Disconnected</span></span>
    </div>
    <div class="controls">
      <div id="authBox" class="gf-group">
        <input id="tok" type="password" placeholder="Admin Token" style="width:170px" onkeydown="if(event.key==='Enter')connectNow()">
        <button class="pri" onclick="connectNow()">Connect</button>
      </div>
      <select id="timeRange" onchange="onRangeChange()" title="Time window">
        <option value="300" selected>Last 5m</option>
        <option value="900">Last 15m</option>
        <option value="3600">Last 1h</option>
      </select>
      <div class="gf-group">
        <button class="icon-btn" onclick="refresh(true)" title="Refresh dashboard now">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>
        </button>
        <select id="refreshSel" onchange="resetTimer()" title="Auto-refresh cadence (Auto = 1s on 5m, 2s on 15m, 5s on 1h)">
          <option value="auto" selected>Auto</option>
          <option value="1000">1s</option>
          <option value="2000">2s</option>
          <option value="5000">5s</option>
          <option value="10000">10s</option>
          <option value="30000">30s</option>
          <option value="0">Off</option>
        </select>
      </div>
    </div>
  </header>

  <!-- Main Workspace -->
  <main class="work">
    <!-- VIEW 1: OVERVIEW DASHBOARD -->
    <section id="v-overview" class="view active">
      <div class="grid">
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Online Players</span><span class="panel-sub" id="sCap">/100</span></div>
          <div class="stat-bd"><div class="stat-val t-green" id="sOnline">-</div><div class="stat-meta" id="sKnown">0 known chars</div><svg class="spark" id="spOnline" viewBox="0 0 120 26" preserveAspectRatio="none"></svg></div>
        </div>
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Active Sessions</span><span class="panel-sub">HTTP+TCP</span></div>
          <div class="stat-bd"><div class="stat-val t-blue" id="sSess">-</div><div class="stat-meta" id="sRest">0 resting</div><svg class="spark" id="spSess" viewBox="0 0 120 26" preserveAspectRatio="none"></svg></div>
        </div>
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Events / min</span><span class="panel-sub">audit</span></div>
          <div class="stat-bd"><div class="stat-val t-orange" id="sEv">-</div><div class="stat-meta" id="sSync">sync: -</div><svg class="spark" id="spEv" viewBox="0 0 120 26" preserveAspectRatio="none"></svg></div>
        </div>
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Active Rules</span><span class="panel-sub">bans/mutes</span></div>
          <div class="stat-bd"><div class="stat-val" id="sBans">-</div><div class="stat-meta">SQLite blacklist</div><svg class="spark" id="spBans" viewBox="0 0 120 26" preserveAspectRatio="none"></svg></div>
        </div>
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Access Gate</span><span class="panel-sub" id="sReg">Global</span></div>
          <div class="stat-bd"><div class="stat-val" id="sGate" style="font-size:18px">-</div><div class="stat-meta" id="sDirCnt">1 server in dir</div></div>
        </div>
        <div class="panel c2">
          <div class="panel-hd"><span class="panel-title">Server Uptime</span><span class="panel-sub">durango-rs</span></div>
          <div class="stat-bd"><div class="stat-val" id="sUp" style="font-size:20px">-</div><div class="stat-meta" id="sRate">Auto (1s)</div></div>
        </div>

        <!-- Time-Series Telemetry Panel -->
        <div class="panel c12">
          <div class="panel-hd">
            <span class="panel-title">Real-Time Server Telemetry <span class="panel-sub" id="tsWindow">Last 5m · 1s samples</span></span>
            <span class="panel-sub" id="tsPts">0 pts</span>
          </div>
          <div class="panel-bd flush">
            <div class="ts-wrap"><svg class="ts-svg" id="tsSvg" viewBox="0 0 1000 190" preserveAspectRatio="none"></svg></div>
            <div class="ts-legend" id="tsLegend">
              <span class="leg-item"><span class="leg-sw" style="background:var(--green)"></span>Online Players <b id="lgOn">last: 0</b></span>
              <span class="leg-item"><span class="leg-sw" style="background:var(--blue-hi)"></span>Events / min <b id="lgEv">last: 0</b></span>
              <span class="leg-item"><span class="leg-sw" style="background:var(--orange)"></span>Active Sessions <b id="lgSe">last: 0</b></span>
            </div>
          </div>
        </div>

        <!-- Bottom Split: Live Players + Loki Live Tail -->
        <div class="panel c7">
          <div class="panel-hd">
            <span class="panel-title">Live Players &amp; Zones</span>
            <button class="xs" onclick="setView('players',document.querySelector('[data-view=players]'))">Full Table →</button>
          </div>
          <div class="panel-bd flush tbl-wrap">
            <table><thead><tr><th>Status</th><th>Character</th><th>Account ID</th><th>Zone</th><th>Vitals</th><th>Actions</th></tr></thead><tbody id="ovPlayers"></tbody></table>
          </div>
        </div>
        <div class="panel c5">
          <div class="panel-hd">
            <span class="panel-title">Live Event Stream <span class="panel-sub">Loki tail</span></span>
            <button class="xs" onclick="setView('logs',document.querySelector('[data-view=logs]'))">Explore →</button>
          </div>
          <div class="panel-bd flush">
            <div class="loki-list compact" id="ovLogs"></div>
          </div>
        </div>
      </div>
    </section>

    <!-- VIEW 2: PLAYERS TABLE -->
    <section id="v-players" class="view">
      <div class="panel">
        <div class="panel-hd">
          <span class="panel-title">Players &amp; Active Sessions <span class="panel-sub" id="plCount">0 total</span></span>
          <input id="pSearch" placeholder="Filter name, usr_id, zone, IP..." oninput="renderPlayers()" style="width:260px">
        </div>
        <div class="panel-bd flush tbl-wrap">
          <table><thead><tr><th>State</th><th>Character</th><th>Account ID</th><th>Zone Code</th><th>Coords</th><th>Vitals / Wallet</th><th>Client IP</th><th>Actions</th></tr></thead><tbody id="ptbl"></tbody></table>
        </div>
      </div>
    </section>

    <!-- VIEW 3: EXPLORE LOGS (LOKI) -->
    <section id="v-logs" class="view">
      <div class="panel">
        <div class="panel-hd">
          <span class="panel-title">Explore · Audit &amp; Activity Logs <span class="panel-sub" id="logCnt">0 lines</span> <span class="panel-sub" id="logDisk"></span></span>
          <div class="row">
            <input id="lSearch" placeholder='Log query (player, item, IP, zone)...' oninput="refresh(false)" style="width:240px">
            <button class="xs" onclick="exportLogs()" title="Download filtered logs as NDJSON">Export .jsonl</button>
            <button id="tailBtn" class="xs pri" onclick="toggleTail()">Live Tail: ON</button>
          </div>
        </div>
        <div class="loki-bar" id="catPills">
          <button class="cat-btn active" onclick="setCat('ALL',this)">ALL</button>
          <button class="cat-btn" onclick="setCat('SERVER',this)">SERVER</button>
          <button class="cat-btn" onclick="setCat('AUTH',this)">AUTH</button>
          <button class="cat-btn" onclick="setCat('WORLD',this)">WORLD</button>
          <button class="cat-btn" onclick="setCat('GATHER',this)">GATHER</button>
          <button class="cat-btn" onclick="setCat('CRAFT',this)">CRAFT</button>
          <button class="cat-btn" onclick="setCat('BUILD',this)">BUILD</button>
          <button class="cat-btn" onclick="setCat('DOMAIN',this)">DOMAIN</button>
          <button class="cat-btn" onclick="setCat('COMBAT',this)">COMBAT</button>
          <button class="cat-btn" onclick="setCat('SURVIVAL',this)">SURVIVAL</button>
          <button class="cat-btn" onclick="setCat('EQUIP',this)">EQUIP</button>
          <button class="cat-btn" onclick="setCat('ITEM',this)">ITEM</button>
          <button class="cat-btn" onclick="setCat('SKILL',this)">SKILL</button>
          <button class="cat-btn" onclick="setCat('SOCIAL',this)">SOCIAL</button>
          <button class="cat-btn" onclick="setCat('MARKET',this)">MARKET</button>
          <button class="cat-btn" onclick="setCat('CHAT',this)">CHAT</button>
          <button class="cat-btn" onclick="setCat('MODERATION',this)">MODERATION</button>
        </div>
        <div class="loki-list" id="ltbl"></div>
      </div>
    </section>

    <!-- VIEW 4: MODERATION -->
    <section id="v-moderation" class="view">
      <div class="panel">
        <div class="panel-hd"><span class="panel-title">Add Blacklist / Ban / Mute Rule</span></div>
        <div class="panel-bd row">
          <select id="btype"><option value="character">Character (+Account)</option><option value="account">Account Key</option><option value="ip">IP / CIDR</option><option value="mute">Chat Mute</option></select>
          <input id="btarget" placeholder="Character name, account key, or IP/CIDR" style="min-width:240px">
          <input id="breason" placeholder="Reason (shown to player)" style="min-width:210px">
          <input id="bhours" type="number" placeholder="Hours (blank=perm)" style="width:140px">
          <label class="row" style="gap:4px;color:var(--muted)"><input id="bip" type="checkbox" style="height:auto"> Also ban last IP</label>
          <button class="danger" onclick="addBan()">Apply Rule</button>
        </div>
      </div>
      <div class="panel">
        <div class="panel-hd"><span class="panel-title">Active Bans &amp; Mutes</span></div>
        <div class="panel-bd flush tbl-wrap">
          <table><thead><tr><th>Rule ID</th><th>Type</th><th>Target</th><th>Reason</th><th>Expires</th><th>Action</th></tr></thead><tbody id="btbl"></tbody></table>
        </div>
      </div>
    </section>

    <!-- VIEW 5: SERVER ADMINISTRATION -->
    <section id="v-settings" class="view">
      <div class="panel">
        <div class="panel-hd"><span class="panel-title">Server Configuration</span></div>
        <div class="panel-bd" style="display:flex;flex-direction:column;gap:8px">
          <div class="row">
            <input id="sname" placeholder="Server Display Name" style="min-width:250px">
            <input id="jpw" type="password" placeholder="New Join Password (leave blank to keep)" style="min-width:250px">
            <button class="pri" onclick="saveCfg()">Save Configuration</button>
            <button onclick="clearPw()">Remove Join Password</button>
          </div>
          <textarea id="snotice" rows="2" placeholder="Server Notice / Welcome Banner"></textarea>
        </div>
      </div>
      <div class="panel">
        <div class="panel-hd"><span class="panel-title">Federated Directory <span class="panel-sub">/servers.json</span></span></div>
        <div class="panel-bd flush tbl-wrap">
          <table><thead><tr><th>Server</th><th>Region</th><th>Players</th><th>Access</th><th>Gateway</th><th>Action</th></tr></thead><tbody id="dtbl"></tbody></table>
        </div>
      </div>
    </section>
  </main>
</div>
<div id="toast" class="toast"></div>
<script>
const $=id=>document.getElementById(id);
let timer=null,logCat='ALL',liveTail=true,cfgLoaded=false,inFlight=false,lastSnap=null;
const viewTitles={overview:'Server Overview',players:'Players & Sessions',logs:'Explore · Audit Logs',moderation:'Moderation & Blacklist',settings:'Administration'};
$('tok').value=sessionStorage.getItem('durango_admin_tok')||'';
const esc=s=>String(s??'').replace(/[&<>"'\\]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;','\\':'&#92;'}[c]));
const fmtTime=ts=>ts?new Date(ts*1000).toLocaleTimeString([],{hour12:false,hour:'2-digit',minute:'2-digit',second:'2-digit'}):'-';
const fmtUp=s=>{const d=Math.floor(s/86400),h=Math.floor((s%86400)/3600),m=Math.floor((s%3600)/60),sec=s%60;return d>0?`${d}d ${h}h ${m}m`:h>0?`${h}h ${m}m`:`${m}m ${sec}s`;};
const hdrs=()=>({'Content-Type':'application/json','X-Admin-Token':$('tok').value});
function toast(m){const t=$('toast');t.textContent=m;t.classList.add('show');clearTimeout(t._tm);t._tm=setTimeout(()=>t.classList.remove('show'),1800);}
function copyVal(v,lbl){navigator.clipboard?.writeText(v).then(()=>toast(`Copied ${lbl}: ${v}`)).catch(()=>{});}
async function api(p,opt={}){const r=await fetch(p,{...opt,headers:{...hdrs(),...(opt.headers||{})}});const j=await r.json().catch(()=>({}));if(!r.ok)throw new Error(j.error||('HTTP '+r.status));return j;}
function setView(id,el){document.querySelectorAll('.view').forEach(v=>v.classList.remove('active'));document.querySelectorAll('.nav-btn').forEach(b=>b.classList.remove('active'));$('v-'+id).classList.add('active');if(el)el.classList.add('active');$('crumbView').textContent=viewTitles[id]||id;}
function toggleTokenBox(){const b=$('authBox');b.style.display=b.style.display==='none'?'inline-flex':'none';if(b.style.display!=='none')$('tok').focus();}
function toggleTail(){liveTail=!liveTail;$('tailBtn').textContent='Live Tail: '+(liveTail?'ON':'PAUSED');$('tailBtn').classList.toggle('pri',liveTail);}
function setCat(c,el){logCat=c;document.querySelectorAll('#catPills .cat-btn').forEach(b=>b.classList.remove('active'));el.classList.add('active');refresh(false);}
function isTyping(){const a=document.activeElement;return a&&(a.tagName==='INPUT'||a.tagName==='TEXTAREA')&&a.id!=='pSearch'&&a.id!=='lSearch';}
function effectiveMs(){
  const v=$('refreshSel').value;
  if(v==='0')return 0;
  if(v==='auto'){const r=parseInt($('timeRange').value,10);return r<=300?1000:r<=900?2000:5000;}
  return parseInt(v,10);
}
function updateRateLabel(){
  const v=$('refreshSel').value,ms=effectiveMs();
  $('sRate').textContent=ms===0?'Refresh: Off':v==='auto'?`Auto (${ms/1000}s)`:`Every ${ms/1000}s`;
  const r=parseInt($('timeRange').value,10);
  $('tsWindow').textContent=(r<=300?'Last 5m':r<=900?'Last 15m':'Last 1h')+' · 1s samples';
}
function onRangeChange(){updateRateLabel();resetTimer();refresh(false);}
function connectNow(){sessionStorage.setItem('durango_admin_tok',$('tok').value);cfgLoaded=false;refresh(true);resetTimer();}
function resetTimer(){if(timer)clearInterval(timer);updateRateLabel();const ms=effectiveMs();if(ms>0){timer=setInterval(()=>{if(!isTyping()&&!document.hidden)refresh(false);},ms);}}
function catTag(c){const m={SERVER:'tg-info',AUTH:'tg-err',WORLD:'tg-ok',GATHER:'tg-ok',CRAFT:'tg-info',BUILD:'tg-info',DOMAIN:'tg-info',COMBAT:'tg-warn',SURVIVAL:'tg-ok',EQUIP:'tg-info',ITEM:'tg-info',SKILL:'tg-purp',SOCIAL:'tg-purp',MARKET:'tg-warn',CHAT:'tg-purp',MODERATION:'tg-err'};return `<span class="tag ${m[c]||'tg-off'}">${esc(c)}</span>`;}
function drawSpark(elId,vals,color){
  const el=$(elId);if(!el)return;
  if(!vals||vals.length<2){el.innerHTML='';return;}
  const max=Math.max(1,...vals),w=120,h=24;
  const pts=vals.map((v,i)=>`${(i/(vals.length-1))*w},${h-(v/max)*(h-3)}`).join(' ');
  el.innerHTML=`<polygon points="0,${h} ${pts} ${w},${h}" fill="${color}" fill-opacity="0.14"/><polyline points="${pts}" fill="none" stroke="${color}" stroke-width="1.5"/>`;
}
function drawTimeSeries(metrics){
  const svg=$('tsSvg');if(!svg)return;
  $('tsPts').textContent=(metrics?.length||0)+' pts';
  if(!metrics||metrics.length<2){svg.innerHTML='<text x="500" y="98" fill="#6e737d" font-size="12" font-family="monospace" text-anchor="middle">Collecting telemetry samples...</text>';return;}
  const W=1000,H=190,padL=36,padR=12,padT=12,padB=24,cw=W-padL-padR,ch=H-padT-padB;
  const onV=metrics.map(m=>m.online||0),evV=metrics.map(m=>m.events_1m||0),seV=metrics.map(m=>m.sessions||0);
  const maxY=Math.max(2,...onV,...evV,...seV);
  const xAt=i=>padL+(i/(metrics.length-1))*cw;
  const yAt=v=>padT+ch-(v/maxY)*ch;
  let grid='';
  for(let g=0;g<=4;g++){
    const gy=padT+(g/4)*ch,val=Math.round(maxY*(1-g/4));
    grid+=`<line x1="${padL}" y1="${gy}" x2="${W-padR}" y2="${gy}" stroke="#2c3235" stroke-dasharray="2,3"/>`;
    grid+=`<text x="${padL-6}" y="${gy+3}" fill="#6e737d" font-size="10" font-family="monospace" text-anchor="end">${val}</text>`;
  }
  const tickIdxs=[0,Math.floor(metrics.length/4),Math.floor(metrics.length/2),Math.floor(metrics.length*3/4),metrics.length-1];
  for(const ti of tickIdxs){
    const tx=xAt(ti);
    grid+=`<text x="${tx}" y="${H-6}" fill="#6e737d" font-size="10" font-family="monospace" text-anchor="middle">${fmtTime(metrics[ti].ts)}</text>`;
  }
  const poly=arr=>arr.map((v,i)=>`${xAt(i).toFixed(1)},${yAt(v).toFixed(1)}`).join(' ');
  const area=(arr,col)=>`<polygon points="${padL},${padT+ch} ${poly(arr)} ${W-padR},${padT+ch}" fill="${col}" fill-opacity="0.10"/><polyline points="${poly(arr)}" fill="none" stroke="${col}" stroke-width="1.8"/>`;
  svg.innerHTML=grid+area(evV,'#5794f2')+area(seV,'#ff9830')+area(onV,'#73bf69');
  const stat=a=>`last:${a[a.length-1]} max:${Math.max(...a)}`;
  $('lgOn').textContent=stat(onV);$('lgEv').textContent=stat(evV);$('lgSe').textContent=stat(seV);
}
function renderLogsHtml(logs){
  if(!logs||!logs.length)return '<div class="empty">No matching audit log lines in window.</div>';
  return logs.map(l=>`<div class="log-row lv-${esc(l.category)}">
    <span class="log-ts">${fmtTime(l.ts)}</span>
    <span>${catTag(l.category)}</span>
    <span class="log-actor" title="${esc(l.actor)}">${esc(l.actor||'system')}</span>
    <span class="log-msg">${esc(l.summary)}${l.region_code?`<span class="log-kv">zone=${esc(l.region_code)}</span>`:''}${l.actor_ip?`<span class="log-kv">ip=${esc(l.actor_ip)}</span>`:''}</span>
  </div>`).join('');
}
function renderPlayers(){
  if(!lastSnap)return;
  const q=($('pSearch')?.value||'').toLowerCase();
  const onSet=new Set(lastSnap.online.map(p=>p.entity_id));
  const all=[...lastSnap.online.map(p=>({...p,live:true})),...lastSnap.recent.filter(r=>!onSet.has(r.entity_id)).map(r=>({...r,ip:r.last_ip,live:false}))];
  $('plCount').textContent=`${lastSnap.online.length} online · ${all.length} total`;
  const filtered=all.filter(p=>!q||`${p.name} ${p.account_id} ${p.owner_key} ${p.ip||''} ${p.region_code||''} ${p.region_label||''}`.toLowerCase().includes(q));
  $('ptbl').innerHTML=filtered.length?filtered.map(p=>`<tr>
    <td>${p.live?'<span class="tag tg-ok">ONLINE</span>':'<span class="tag tg-off">OFFLINE</span>'}${p.is_resting?' <span class="tag tg-info">REST</span>':''}</td>
    <td><strong style="color:var(--text-bright)">${esc(p.name)}</strong><span class="sub mono">Lv.${p.level||1}</span></td>
    <td><span class="code-chip" title="Click to copy Account ID" data-act="copy" data-val="${esc(p.owner_key)}" data-lbl="account_id">${esc(p.owner_key)}</span></td>
    <td><span class="code-chip" title="Click to copy Zone Code" data-act="copy" data-val="${esc(p.region_id)}" data-lbl="zone">${esc(p.region_id)}</span><span class="sub">${esc(p.region_label||'')}</span></td>
    <td class="mono">${p.live?`(${p.tile_x}, ${p.tile_y})`:'—'}</td>
    <td class="mono">${p.live?`HP ${Math.round(p.life)} · Fat ${Math.round(p.fatigue)}`:'—'}<span class="sub">${(p.t_stone||0).toLocaleString()} T-Stone</span></td>
    <td><span class="code-chip" data-act="copy" data-val="${esc(p.ip||'')}" data-lbl="IP">${esc(p.ip||'—')}</span></td>
    <td class="row">${p.live?`<button class="xs" data-act="kick" data-target="${esc(p.entity_id)}">Kick</button>`:''}
      <button class="xs danger" data-act="qban" data-btype="character" data-target="${esc(p.name)}">Ban</button>
      ${p.ip?`<button class="xs danger" data-act="qban" data-btype="ip" data-target="${esc(p.ip)}">Ban IP</button>`:''}
      <button class="xs warn" data-act="qban" data-btype="mute" data-target="${esc(p.name)}">Mute</button></td>
  </tr>`).join(''):'<tr><td colspan="8" class="empty">No matching players.</td></tr>';

  $('ovPlayers').innerHTML=all.slice(0,8).length?all.slice(0,8).map(p=>`<tr>
    <td>${p.live?'<span class="tag tg-ok">ONLINE</span>':'<span class="tag tg-off">OFF</span>'}</td>
    <td><strong style="color:var(--text-bright)">${esc(p.name)}</strong> <span class="mono" style="color:var(--muted)">Lv.${p.level||1}</span></td>
    <td><span class="code-chip" data-act="copy" data-val="${esc(p.owner_key)}" data-lbl="account_id">${esc(p.owner_key)}</span></td>
    <td><span class="code-chip" title="${esc(p.region_label)}">${esc(p.region_id)}</span></td>
    <td class="mono">${p.live?`HP ${Math.round(p.life)} · Fat ${Math.round(p.fatigue)}`:'—'}</td>
    <td class="row">${p.live?`<button class="xs" data-act="kick" data-target="${esc(p.entity_id)}">Kick</button>`:''}<button class="xs danger" data-act="qban" data-btype="character" data-target="${esc(p.name)}">Ban</button></td>
  </tr>`).join(''):'<tr><td colspan="6" class="empty">No players connected yet.</td></tr>';
}
async function refresh(forceCfg){
  if(inFlight)return;
  inFlight=true;
  try{
    const q=encodeURIComponent($('lSearch')?.value||'');
    const rng=encodeURIComponent($('timeRange')?.value||'300');
    const s=await api(`/admin/snapshot?category=${encodeURIComponent(logCat)}&q=${q}&limit=150&range_secs=${rng}`);
    lastSnap=s;
    $('authBox').style.display='none';
    $('srvName').textContent=s.config.server_name||'Operations';
    $('statusPill').className='live-pill ok';
    $('statusTxt').textContent='Live · '+fmtTime(s.server_time);
    $('sOnline').textContent=s.online.length;
    $('sCap').textContent='/'+s.config.max_players;
    $('sKnown').textContent=`${s.recent.length} known characters`;
    $('sSess').textContent=s.sessions_count||0;
    const resting=s.online.filter(p=>p.is_resting).length;
    $('sRest').textContent=`${resting} resting now`;
    const lastEv=s.metrics.length?s.metrics[s.metrics.length-1].events_1m:0;
    $('sEv').textContent=lastEv;
    $('sSync').textContent='sync '+fmtTime(s.server_time);
    $('sBans').textContent=s.bans.length;
    $('sBans').className='stat-val '+(s.bans.length>0?'t-red':'t-green');
    $('sReg').textContent=s.config.region||'Global';
    $('sGate').innerHTML=s.config.password_required?'<span class="t-orange">PASSWORD</span>':'<span class="t-green">OPEN</span>';
    $('sDirCnt').textContent=`${(s.directory||[]).length} server(s) in dir`;
    $('sUp').textContent=fmtUp(s.uptime_secs||0);

    drawSpark('spOnline',s.metrics.map(m=>m.online),'#73bf69');
    drawSpark('spSess',s.metrics.map(m=>m.sessions),'#5794f2');
    drawSpark('spEv',s.metrics.map(m=>m.events_1m),'#ff9830');
    drawSpark('spBans',s.metrics.map(m=>m.bans),'#f2495c');
    drawTimeSeries(s.metrics);
    renderPlayers();

    if(liveTail||forceCfg){
      $('logCnt').textContent=s.logs.length+' lines';
      const kb=((s.log_disk_bytes||0)/1024).toFixed(1);
      $('logDisk').textContent=`· ${kb} KB (${s.log_archives||0} .gz archives)`;
      $('ltbl').innerHTML=renderLogsHtml(s.logs);
      $('ovLogs').innerHTML=renderLogsHtml(s.logs.slice(0,25));
    }
    if(forceCfg||!cfgLoaded){$('sname').value=s.config.server_name||'';$('snotice').value=s.config.notice||'';cfgLoaded=true;}
    $('btbl').innerHTML=s.bans.length?s.bans.map(b=>`<tr><td class="mono">#${b.id}</td><td><span class="tag tg-err">${esc(b.ban_type.toUpperCase())}</span></td><td><span class="code-chip">${esc(b.target_value)}</span></td><td>${esc(b.reason||'—')}</td><td class="mono">${b.expires_at?new Date(b.expires_at*1000).toLocaleString():'Permanent'}</td><td><button class="xs" data-act="unban" data-id="${b.id}">Unban</button></td></tr>`).join(''):'<tr><td colspan="6" class="empty">No active bans or mutes.</td></tr>';
    $('dtbl').innerHTML=(s.directory||[]).map(d=>`<tr><td><strong style="color:var(--text-bright)">${esc(d.name)}</strong></td><td class="mono">${esc(d.region)}</td><td class="mono">${d.online_players}/${d.max_players}</td><td>${d.password_required?'<span class="tag tg-warn">LOCKED</span>':'<span class="tag tg-ok">OPEN</span>'}</td><td><span class="code-chip">${esc(d.gateway)}</span></td><td>${d.server_id==='self'?'<span class="mono" style="color:var(--muted)">local-host</span>':`<button class="xs danger" data-act="rmdir" data-id="${esc(d.server_id)}">Remove</button>`}</td></tr>`).join('');
  }catch(e){
    $('statusPill').className='live-pill err';
    $('statusTxt').textContent=e.message;
    $('authBox').style.display='inline-flex';
  }finally{inFlight=false;}
}
async function exportLogs(){
  const q=encodeURIComponent($('lSearch')?.value||'');
  const r=await fetch(`/admin/logs/export?category=${encodeURIComponent(logCat)}&q=${q}`,{headers:hdrs()});
  if(!r.ok){toast('Export failed');return;}
  const blob=await r.blob(),a=document.createElement('a');
  a.href=URL.createObjectURL(blob);a.download='durango-audit-logs.jsonl';a.click();
  URL.revokeObjectURL(a.href);toast('Exported audit logs (.jsonl)');
}
async function saveCfg(){const body={server_name:$('sname').value,notice:$('snotice').value};if($('jpw').value)body.join_password=$('jpw').value;await api('/admin/config',{method:'POST',body:JSON.stringify(body)});$('jpw').value='';toast('Configuration saved');refresh(true);}
async function clearPw(){await api('/admin/config',{method:'POST',body:JSON.stringify({join_password:''})});toast('Join password removed');refresh(true);}
async function kick(t){await api('/admin/kick',{method:'POST',body:JSON.stringify({target:t})});toast('Player kicked');refresh(false);}
async function qban(bt,t){const r=prompt('Reason for '+bt+' '+t+':','Violation of server rules');if(r===null)return;await api('/admin/bans',{method:'POST',body:JSON.stringify({ban_type:bt,target:t,reason:r})});toast('Rule applied');refresh(false);}
async function addBan(){const h=parseFloat($('bhours').value);await api('/admin/bans',{method:'POST',body:JSON.stringify({ban_type:$('btype').value,target:$('btarget').value,reason:$('breason').value,duration_hours:isNaN(h)?null:h,include_ip:$('bip').checked})});$('btarget').value='';toast('Ban/mute rule added');refresh(false);}
async function unban(id){await api('/admin/bans/'+id,{method:'DELETE'});toast('Removed rule #'+id);refresh(false);}
async function rmDir(id){await api('/admin/directory/'+encodeURIComponent(id),{method:'DELETE'});toast('Directory entry removed');refresh(false);}
document.addEventListener('click',ev=>{
  const el=ev.target.closest('[data-act]');
  if(!el)return;
  const act=el.dataset.act;
  if(act==='copy')copyVal(el.dataset.val||'',el.dataset.lbl||'');
  else if(act==='kick')kick(el.dataset.target||'');
  else if(act==='qban')qban(el.dataset.btype||'character',el.dataset.target||'');
  else if(act==='unban')unban(el.dataset.id||'');
  else if(act==='rmdir')rmDir(el.dataset.id||'');
});
updateRateLabel();
refresh(true);resetTimer();
</script></body></html>"##)
}
