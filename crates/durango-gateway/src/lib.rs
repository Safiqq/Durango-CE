use std::collections::HashMap;
use std::path::{Component, Path, PathBuf};
use std::sync::Arc;

use axum::body::Bytes;
use axum::extract::{Path as AxumPath, Query, State};
use axum::http::{header, HeaderMap, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Json, Router};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use tokio::sync::RwLock;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct GatewayConfig {
    pub bind_addr: String,
    pub public_host: String,
    pub game_tcp_port: u16,
    pub server_name: String,
    pub server_mode: String,
    pub assets_dir: Option<PathBuf>,
}

impl Default for GatewayConfig {
    fn default() -> Self {
        Self {
            bind_addr: "127.0.0.1:8190".to_string(),
            public_host: "127.0.0.1".to_string(),
            game_tcp_port: 8191,
            server_name: "Durango-CE Local Server".to_string(),
            server_mode: "Survival".to_string(),
            assets_dir: None,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PlayerInfo {
    pub player_level: i32,
    pub disconnected_at: f64,
    pub player_name: String,
    pub player_entity_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub deletes_at: Option<f64>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AccountResponse {
    pub players: Vec<PlayerInfo>,
    pub player_slot_count: usize,
    pub max_player_slot_count: usize,
}

#[derive(Debug, Clone)]
pub struct SessionEntry {
    pub owner_key: String,
    pub entity_id: String,
    pub created_at: std::time::Instant,
}

#[derive(Default)]
struct GatewayStore {
    accounts: HashMap<String, Vec<PlayerInfo>>,
    sessions: HashMap<String, SessionEntry>,
}

impl GatewayStore {
    fn prune_sessions(&mut self) {
        const TTL: std::time::Duration = std::time::Duration::from_secs(86_400);
        const MAX_SESSIONS: usize = 4096;
        let now = std::time::Instant::now();
        self.sessions
            .retain(|_, s| now.duration_since(s.created_at) < TTL);
        if self.sessions.len() >= MAX_SESSIONS {
            let mut oldest: Vec<(String, std::time::Instant)> = self
                .sessions
                .iter()
                .map(|(k, v)| (k.clone(), v.created_at))
                .collect();
            oldest.sort_by_key(|(_, ts)| *ts);
            let drop_n = self.sessions.len() - (MAX_SESSIONS / 2);
            for (k, _) in oldest.into_iter().take(drop_n) {
                self.sessions.remove(&k);
            }
        }
    }
}

#[derive(Clone)]
pub struct GatewayState {
    pub config: Arc<GatewayConfig>,
    store: Arc<RwLock<GatewayStore>>,
}

impl GatewayState {
    pub fn new(config: GatewayConfig) -> Self {
        Self {
            config: Arc::new(config),
            store: Arc::new(RwLock::new(GatewayStore::default())),
        }
    }
}

pub fn build_router(state: GatewayState) -> Router {
    Router::new()
        .route("/servers.json", get(get_servers_json))
        .route("/status", get(get_status))
        .route("/health", get(get_health))
        .route("/knock", get(get_knock))
        .route("/notice", get(get_notice))
        .route("/events/calendar", get(get_events_calendar))
        .route("/cs/answer", get(get_cs_answer))
        .route("/accounts", post(post_accounts))
        .route("/sessions", post(post_sessions))
        .route("/admission", get(get_admission))
        .route("/entry", get(get_entry))
        .route("/assets/*path", get(get_asset_file))
        .route("/live/:platform/Info.5.2.1.json", get(get_bundle_info))
        .with_state(state)
}

fn root_url(headers: &HeaderMap, config: &GatewayConfig) -> String {
    if let Some(host) = headers.get(header::HOST).and_then(|v| v.to_str().ok()) {
        let trimmed = host.trim();
        if !trimmed.is_empty() {
            return format!("http://{trimmed}");
        }
    }
    let port = config
        .bind_addr
        .rsplit(':')
        .next()
        .unwrap_or("8190");
    format!("http://{}:{port}", config.public_host)
}

fn normalize_account_key(raw: Option<&str>) -> Option<String> {
    let trimmed = raw?.trim().to_ascii_lowercase();
    if trimmed.len() < 4 || trimmed.len() > 128 {
        return None;
    }
    if !trimmed
        .bytes()
        .all(|b| b.is_ascii_alphanumeric() || matches!(b, b'-' | b'_'))
    {
        return None;
    }
    Some(trimmed)
}

fn parse_form_or_json(body: &[u8]) -> HashMap<String, String> {
    let mut map = HashMap::new();
    let Ok(text) = std::str::from_utf8(body) else {
        return map;
    };
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
            map.insert(percent_decode(k), percent_decode(v));
        }
    }
    map
}

fn percent_decode(input: &str) -> String {
    let bytes = input.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b'+' => {
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

async fn get_servers_json(
    State(state): State<GatewayState>,
    headers: HeaderMap,
) -> Json<Value> {
    let base = root_url(&headers, &state.config);
    tracing::info!("GET /servers.json -> {base}");
    Json(json!({
        "servers": [
            {
                "name": state.config.server_name,
                "gateway": base
            }
        ]
    }))
}

async fn get_status(State(state): State<GatewayState>) -> Json<Value> {
    let online = state.store.read().await.sessions.len();
    tracing::info!(online, "GET /status -> 200");
    Json(json!({
        "ok": true,
        "online": online,
        "version": "3.3.6",
        "server_mode": state.config.server_mode
    }))
}

async fn get_health(State(state): State<GatewayState>) -> Json<Value> {
    let store = state.store.read().await;
    Json(json!({
        "ok": true,
        "engine": "durango-gateway-rs",
        "version": env!("CARGO_PKG_VERSION"),
        "sessions": store.sessions.len(),
        "accounts": store.accounts.len(),
        "assets_dir": state.config.assets_dir.as_ref().map(|p| p.display().to_string())
    }))
}

#[derive(Deserialize)]
struct KnockQuery {
    platform: Option<String>,
    version: Option<String>,
}

async fn get_knock(
    State(state): State<GatewayState>,
    headers: HeaderMap,
    Query(q): Query<KnockQuery>,
) -> Json<Value> {
    let platform = match q.platform.as_deref().map(|s| s.to_ascii_lowercase()).as_deref() {
        Some("android") => "android",
        Some("ios") => "ios",
        _ => "windows",
    };
    let root = root_url(&headers, &state.config);
    tracing::info!(platform, version = ?q.version, "GET /knock -> 200");
    Json(json!({
        "server_version": "5.2.1",
        "compatible": true,
        "download_url": "",
        "assetbundle_index_url": format!("{root}/live/{platform}/Info.5.2.1.json"),
        "assetbundle_url_root": format!("{root}/live/{platform}/"),
        "server_mode": state.config.server_mode,
        "cheats_allowed": state.config.server_mode.eq_ignore_ascii_case("sandbox")
    }))
}

async fn get_notice() -> Json<Value> {
    Json(json!({}))
}

async fn get_events_calendar() -> Json<Value> {
    tracing::info!("GET /events/calendar -> 200");
    Json(json!({
        "ok": true,
        "events": []
    }))
}

async fn get_cs_answer() -> Json<Value> {
    tracing::info!("GET /cs/answer -> 200");
    Json(json!({
        "updated_at": Value::Null
    }))
}

async fn get_bundle_info(AxumPath(platform): AxumPath<String>) -> Json<Value> {
    tracing::info!(%platform, "GET /live/{platform}/Info.5.2.1.json -> 200");
    Json(json!({
        "PreloadHash": "c4408d73cff6e7ad670c0c93a2b04385",
        "PreloadCrc": "c4408d73cff6e7ad670c0c93a2b04385",
        "ItemList": [],
        "FileList": []
    }))
}

async fn post_accounts(
    State(state): State<GatewayState>,
    body: Bytes,
) -> Json<AccountResponse> {
    let form = parse_form_or_json(&body);
    let key = normalize_account_key(form.get("account_id").map(String::as_str));
    let Some(owner_key) = key else {
        tracing::info!("POST /accounts (no account_id) -> empty account");
        return Json(AccountResponse {
            players: Vec::new(),
            player_slot_count: 10,
            max_player_slot_count: 10,
        });
    };

    let players = state
        .store
        .read()
        .await
        .accounts
        .get(&owner_key)
        .cloned()
        .unwrap_or_default();
    let slots = 10usize.max(players.len() + 1);
    tracing::info!(
        owner = &owner_key[..owner_key.len().min(8)],
        characters = players.len(),
        "POST /accounts -> 200"
    );
    Json(AccountResponse {
        players,
        player_slot_count: slots,
        max_player_slot_count: slots,
    })
}

async fn post_sessions(
    State(state): State<GatewayState>,
    body: Bytes,
) -> Response {
    let form = parse_form_or_json(&body);
    let Some(owner_key) = normalize_account_key(form.get("account_id").map(String::as_str)) else {
        return (
            StatusCode::UNAUTHORIZED,
            Json(json!({ "error": "no_account_key" })),
        )
            .into_response();
    };

    let entity_id = Uuid::new_v4().to_string();
    let token = Uuid::new_v4().simple().to_string();
    {
        let mut store = state.store.write().await;
        store.prune_sessions();
        store.sessions.insert(
            token.clone(),
            SessionEntry {
                owner_key: owner_key.clone(),
                entity_id: entity_id.clone(),
                created_at: std::time::Instant::now(),
            },
        );
    }
    tracing::info!(
        owner = &owner_key[..owner_key.len().min(8)],
        %entity_id,
        "POST /sessions -> 200"
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

async fn get_admission() -> Json<Value> {
    Json(json!({ "admitted": true }))
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

async fn get_entry(
    State(state): State<GatewayState>,
    headers: HeaderMap,
) -> Json<Value> {
    let host = headers
        .get(header::HOST)
        .and_then(|v| v.to_str().ok())
        .map(host_without_port)
        .filter(|h| !h.is_empty())
        .unwrap_or(&state.config.public_host);
    let tcp_addr = format!("{host}:{}", state.config.game_tcp_port);
    tracing::info!(%tcp_addr, "GET /entry -> 200");
    Json(json!({
        "frontend_addresses": [tcp_addr],
        "radiotower_addresses": [tcp_addr],
        "cluster_mode": "Online",
        "server_mode": state.config.server_mode,
        "cheats_allowed": state.config.server_mode.eq_ignore_ascii_case("sandbox")
    }))
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

async fn get_asset_file(
    State(state): State<GatewayState>,
    AxumPath(rel): AxumPath<String>,
) -> Response {
    let Some(base) = &state.config.assets_dir else {
        return (StatusCode::NOT_FOUND, "assets_dir not configured").into_response();
    };
    if rel.contains("..") {
        return (StatusCode::BAD_REQUEST, "invalid asset path").into_response();
    }
    let Some(path) = resolve_safe_asset_path(base, &rel) else {
        tracing::warn!(%rel, "GET /assets/{rel} -> 404");
        return (StatusCode::NOT_FOUND, "asset not found").into_response();
    };
    match tokio::fs::read(&path).await {
        Ok(bytes) => {
            tracing::debug!(%rel, bytes = bytes.len(), "GET /assets/{rel} -> 200");
            (
                StatusCode::OK,
                [(header::CONTENT_TYPE, "application/json; charset=utf-8")],
                bytes,
            )
                .into_response()
        }
        Err(_) => (StatusCode::INTERNAL_SERVER_ERROR, "read failed").into_response(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use axum::body::Body;
    use axum::http::Request;
    use http_body_util::BodyExt;
    use tower::ServiceExt;

    async fn json_body(resp: Response) -> Value {
        let bytes = resp.into_body().collect().await.unwrap().to_bytes();
        serde_json::from_slice(&bytes).unwrap()
    }

    #[tokio::test]
    async fn gateway_serves_v336_endpoints_cleanly() {
        let app = build_router(GatewayState::new(GatewayConfig::default()));

        let r = app
            .clone()
            .oneshot(Request::get("/status").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let j = json_body(r).await;
        assert_eq!(j["ok"], true);
        assert_eq!(j["version"], "3.3.6");

        let r = app
            .clone()
            .oneshot(Request::get("/servers.json").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let j = json_body(r).await;
        assert_eq!(j["servers"][0]["gateway"], "http://127.0.0.1:8190");

        let r = app
            .clone()
            .oneshot(
                Request::get("/knock?platform=windows&version=5.2.1")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let j = json_body(r).await;
        assert_eq!(j["compatible"], true);
        assert_eq!(j["server_version"], "5.2.1");

        let r = app
            .clone()
            .oneshot(Request::get("/events/calendar").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        assert_eq!(json_body(r).await["ok"], true);

        let r = app
            .clone()
            .oneshot(Request::get("/cs/answer").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        assert!(json_body(r).await["updated_at"].is_null());

        let r = app
            .clone()
            .oneshot(
                Request::post("/accounts")
                    .body(Body::from("account_id=ef72cdc18325c48857551c3c43f05ba32c8e580a"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let acc = json_body(r).await;
        assert_eq!(acc["player_slot_count"], 10);
        assert_eq!(acc["max_player_slot_count"], 10);

        let r = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=ef72cdc18325c48857551c3c43f05ba32c8e580a"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let sess = json_body(r).await;
        assert!(sess["user_id"].as_str().is_some());
        assert!(sess["session_token"].as_str().is_some());

        let r = app
            .clone()
            .oneshot(Request::get("/entry").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let entry = json_body(r).await;
        assert_eq!(entry["frontend_addresses"][0], "127.0.0.1:8191");
    }

    #[tokio::test]
    async fn rejects_missing_account_key_and_path_traversal() {
        let mut cfg = GatewayConfig::default();
        cfg.assets_dir = Some(PathBuf::from("/tmp"));
        let app = build_router(GatewayState::new(cfg));

        let r = app
            .clone()
            .oneshot(Request::post("/sessions").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::UNAUTHORIZED);

        let r = app
            .clone()
            .oneshot(
                Request::get("/assets/..%2F..%2Fetc%2Fpasswd")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert!(matches!(
            r.status(),
            StatusCode::BAD_REQUEST | StatusCode::NOT_FOUND
        ));
    }
}
