pub mod attestation;
pub mod catalog;
pub mod db;
pub mod http;
pub mod state;
pub mod tcp;
pub mod terrain;

use std::net::SocketAddr;

use tokio::net::TcpListener;

use crate::http::build_http_router;
use crate::state::{ServerConfig, ServerState};
use crate::tcp::{run_tcp_server, run_wildlife_ai_loop};

pub struct RunningServer {
    pub http_addr: SocketAddr,
    pub tcp_addr: SocketAddr,
    pub state: ServerState,
    http_handle: tokio::task::JoinHandle<()>,
    tcp_handle: tokio::task::JoinHandle<()>,
    ai_handle: Option<tokio::task::JoinHandle<()>>,
    heartbeat_handle: Option<tokio::task::JoinHandle<()>>,
    upstream_sync_handle: Option<tokio::task::JoinHandle<()>>,
}

impl RunningServer {
    pub fn abort(self) {
        self.http_handle.abort();
        self.tcp_handle.abort();
        if let Some(h) = self.ai_handle {
            h.abort();
        }
        if let Some(h) = self.heartbeat_handle {
            h.abort();
        }
        if let Some(h) = self.upstream_sync_handle {
            h.abort();
        }
    }
}

/// Normalizes a user-provided VM host, gateway URL, or full `servers.json` URL
/// into a canonical URL pointing to its `servers.json` endpoint.
pub fn normalize_servers_json_url(raw: &str) -> Option<String> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return None;
    }
    let with_scheme = if trimmed.starts_with("http://") || trimmed.starts_with("https://") {
        trimmed.to_string()
    } else {
        let (host_part, rest) = match trimmed.split_once('/') {
            Some((h, r)) => (h, Some(r)),
            None => (trimmed, None),
        };
        let host_with_port = if host_part.contains(':') {
            host_part.to_string()
        } else {
            format!("{host_part}:8190")
        };
        match rest {
            Some(r) => format!("http://{host_with_port}/{r}"),
            None => format!("http://{host_with_port}"),
        }
    };
    let no_trail = with_scheme.trim_end_matches('/');
    let after_scheme = no_trail
        .strip_prefix("http://")
        .or_else(|| no_trail.strip_prefix("https://"))
        .unwrap_or(no_trail);
    if after_scheme.contains('/') {
        Some(no_trail.to_string())
    } else {
        Some(format!("{no_trail}/servers.json"))
    }
}

/// Parses an upstream `servers.json` payload (`{"servers":[...]}` or `[...]`)
/// into `DirectoryServerEntry` items tagged with `upstream:` IDs.
pub fn parse_upstream_servers_json(raw: &str, now: f64) -> Vec<crate::state::DirectoryServerEntry> {
    let Ok(val) = serde_json::from_str::<serde_json::Value>(raw) else {
        return Vec::new();
    };
    let arr_opt = val
        .get("servers")
        .and_then(|v| v.as_array())
        .or_else(|| val.as_array());
    let Some(arr) = arr_opt else {
        return Vec::new();
    };
    let mut out = Vec::new();
    for item in arr {
        let Some(name) = item.get("name").and_then(|v| v.as_str()).map(str::trim) else {
            continue;
        };
        let Some(gw) = item.get("gateway").and_then(|v| v.as_str()).map(str::trim) else {
            continue;
        };
        if name.is_empty() || gw.is_empty() {
            continue;
        }
        let norm_gw = gw.trim_end_matches('/').to_string();
        let region = item
            .get("region")
            .and_then(|v| v.as_str())
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .unwrap_or("Global")
            .to_string();
        let online = item
            .get("online")
            .or_else(|| item.get("online_players"))
            .and_then(|v| v.as_u64())
            .unwrap_or(0) as usize;
        let max_players = item
            .get("max_players")
            .and_then(|v| v.as_u64())
            .unwrap_or(100) as usize;
        let password_required = item
            .get("password_required")
            .and_then(|v| v.as_bool())
            .unwrap_or(false);
        let pvp_enabled = item
            .get("pvp_enabled")
            .and_then(|v| v.as_bool())
            .unwrap_or(false);
        let verified = item.get("verified").and_then(|v| v.as_bool());
        let version = item
            .get("version")
            .and_then(|v| v.as_str())
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .unwrap_or("3.3.6")
            .to_string();
        out.push(crate::state::DirectoryServerEntry {
            server_id: format!("upstream:{norm_gw}"),
            name: name.to_string(),
            gateway: norm_gw,
            region,
            online_players: online,
            max_players,
            password_required,
            pvp_enabled,
            verified,
            version,
            last_seen_at: now,
        });
    }
    out
}

fn decode_http_response_body(raw: &[u8]) -> Option<String> {
    let sep = raw
        .windows(4)
        .position(|w| w == b"\r\n\r\n")
        .map(|pos| (pos, 4usize))
        .or_else(|| raw.windows(2).position(|w| w == b"\n\n").map(|pos| (pos, 2usize)))?;
    let head = std::str::from_utf8(&raw[..sep.0]).ok()?;
    let status_ok = head
        .lines()
        .next()
        .and_then(|l| l.split_whitespace().nth(1))
        .is_some_and(|code| code.starts_with('2'));
    if !status_ok {
        return None;
    }
    let is_chunked = head
        .lines()
        .any(|l| l.to_ascii_lowercase().contains("transfer-encoding:") && l.to_ascii_lowercase().contains("chunked"));
    let body = &raw[sep.0 + sep.1..];
    if !is_chunked {
        return String::from_utf8(body.to_vec()).ok();
    }
    let mut decoded = Vec::new();
    let mut cur = body;
    loop {
        let crlf = cur.windows(2).position(|w| w == b"\r\n")?;
        let size_str = std::str::from_utf8(&cur[..crlf])
            .ok()?
            .split(';')
            .next()?
            .trim();
        let sz = usize::from_str_radix(size_str, 16).ok()?;
        cur = &cur[crlf + 2..];
        if sz == 0 {
            break;
        }
        if cur.len() < sz {
            return None;
        }
        decoded.extend_from_slice(&cur[..sz]);
        cur = cur.get(sz + 2..)?;
    }
    String::from_utf8(decoded).ok()
}

pub async fn fetch_http_get_url(url: &str) -> Option<String> {
    fetch_upstream_servers_url(url).await
}

async fn fetch_upstream_servers_url(url: &str) -> Option<String> {
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    if let Some(stripped) = url.strip_prefix("http://") {
        let (host_port, path) = match stripped.split_once('/') {
            Some((hp, rest)) => (hp, format!("/{rest}")),
            None => (stripped, "/servers.json".to_string()),
        };
        let connect_addr = if host_port.contains(':') {
            host_port.to_string()
        } else {
            format!("{host_port}:80")
        };
        let Ok(Ok(mut stream)) = tokio::time::timeout(
            std::time::Duration::from_secs(3),
            tokio::net::TcpStream::connect(&connect_addr),
        )
        .await
        else {
            return None;
        };
        let req = format!(
            "GET {path} HTTP/1.1\r\nHost: {host_port}\r\nAccept: application/json\r\nConnection: close\r\n\r\n"
        );
        if stream.write_all(req.as_bytes()).await.is_err() {
            return None;
        }
        let mut buf = Vec::with_capacity(4096);
        let Ok(Ok(_)) = tokio::time::timeout(
            std::time::Duration::from_secs(3),
            stream.read_to_end(&mut buf),
        )
        .await
        else {
            return None;
        };
        return decode_http_response_body(&buf);
    }

    if url.starts_with("https://") {
        let out = tokio::time::timeout(
            std::time::Duration::from_secs(4),
            tokio::process::Command::new("curl")
                .args(["--noproxy", "*", "-fsS", "--max-time", "3", url])
                .output(),
        )
        .await
        .ok()?
        .ok()?;
        if out.status.success() {
            return String::from_utf8(out.stdout).ok();
        }
    }
    None
}

async fn sync_upstream_servers_once(state: &ServerState, urls: &[String]) {
    let now = ServerState::now_unix_secs();
    let mut merged: Vec<crate::state::DirectoryServerEntry> = Vec::new();
    let mut seen_gateways: std::collections::HashSet<String> = std::collections::HashSet::new();
    let mut any_ok = false;

    for url in urls {
        if let Some(body) = fetch_upstream_servers_url(url).await {
            any_ok = true;
            for entry in parse_upstream_servers_json(&body, now) {
                let key = entry.gateway.to_ascii_lowercase();
                if seen_gateways.insert(key) {
                    merged.push(entry);
                }
            }
        } else {
            tracing::debug!(%url, "upstream servers.json fetch failed or unreachable");
        }
    }

    if any_ok {
        let count = merged.len();
        state.replace_upstream_directory_servers(merged).await;
        tracing::info!(count, "synced upstream server directory entries");
    }
}

async fn run_upstream_servers_sync_loop(state: ServerState, urls: Vec<String>) {
    let mut interval = tokio::time::interval(std::time::Duration::from_secs(30));
    // Initial sync already ran in start_server; skip immediate first tick.
    interval.tick().await;
    loop {
        interval.tick().await;
        sync_upstream_servers_once(&state, &urls).await;
    }
}

/// Sends periodic lightweight HTTP/1.1 heartbeats + CA cryptographic attestation proofs
/// to a central Durango-CE CA directory server when `DURANGO_DIRECTORY_URL` is configured.
async fn run_directory_heartbeat_loop(state: ServerState, dir_url: String, http_port: u16) {
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    let server_id = format!(
        "{}:{}",
        state.config.public_host.trim(),
        http_port
    );
    let self_gateway = format!("http://{}:{http_port}", state.config.public_host.trim());
    let base_dir = dir_url.trim_end_matches('/').to_string();
    let Some(stripped) = base_dir.strip_prefix("http://") else {
        tracing::warn!(
            %dir_url,
            "invalid DURANGO_DIRECTORY_URL (plaintext heartbeat only supports http://; skipping)"
        );
        return;
    };
    let (host_port, base_path) = match stripped.split_once('/') {
        Some((hp, rest)) => (hp.to_string(), format!("/{}", rest.trim_end_matches('/'))),
        None => (stripped.to_string(), String::new()),
    };
    let connect_addr = if host_port.contains(':') {
        host_port.clone()
    } else {
        format!("{host_port}:80")
    };
    let post_path = format!("{base_path}/api/directory/heartbeat");

    let mut interval = tokio::time::interval(std::time::Duration::from_secs(30));
    loop {
        interval.tick().await;
        let challenge_url = format!(
            "{base_dir}/api/directory/challenge?server_id={server_id}&gateway={self_gateway}"
        );
        let nonce = fetch_http_get_url(&challenge_url)
            .await
            .and_then(|body| serde_json::from_str::<serde_json::Value>(&body).ok())
            .and_then(|v| v.get("nonce").and_then(|n| n.as_str()).map(ToString::to_string));

        let ts = ServerState::now_unix_secs() as i64;
        let attestation_sig = match (nonce.as_deref(), state.effective_attestation_key()) {
            (Some(n), Some(k)) => Some(crate::attestation::sign_attestation_claim(
                k,
                n,
                &server_id,
                &self_gateway,
                &state.self_exe_sha256,
                &state.assets_digest,
                "3.3.6",
                ts,
            )),
            _ => None,
        };

        let online = state.live_clients.read().await.len();
        let payload = serde_json::json!({
            "server_id": server_id,
            "name": state.effective_server_name(),
            "gateway": self_gateway,
            "region": state.config.region_tag,
            "online_players": online,
            "max_players": state.config.max_players,
            "password_required": state.effective_join_password().is_some(),
            "pvp_enabled": false,
            "version": "3.3.6",
            "nonce": nonce,
            "ts": ts,
            "exe_sha256": state.self_exe_sha256,
            "assets_digest": state.assets_digest,
            "attestation_sig": attestation_sig,
        })
        .to_string();

        let secret_hdr = state
            .config
            .directory_secret
            .as_deref()
            .map(|s| format!("X-Directory-Secret: {s}\r\n"))
            .unwrap_or_default();
        let req = format!(
            "POST {post_path} HTTP/1.1\r\nHost: {host_port}\r\nContent-Type: application/json\r\n{secret_hdr}Content-Length: {}\r\nConnection: close\r\n\r\n{}",
            payload.len(),
            payload
        );

        match tokio::time::timeout(
            std::time::Duration::from_secs(5),
            tokio::net::TcpStream::connect(&connect_addr),
        )
        .await
        {
            Ok(Ok(mut stream)) => {
                if stream.write_all(req.as_bytes()).await.is_ok() {
                    let mut buf = [0u8; 256];
                    let _ = tokio::time::timeout(
                        std::time::Duration::from_secs(3),
                        stream.read(&mut buf),
                    )
                    .await;
                }
            }
            Ok(Err(err)) => {
                tracing::debug!(%connect_addr, %err, "directory heartbeat connect failed");
            }
            Err(_) => {
                tracing::debug!(%connect_addr, "directory heartbeat timed out");
            }
        }
    }
}

pub async fn start_server(
    config: ServerConfig,
) -> Result<RunningServer, Box<dyn std::error::Error + Send + Sync>> {
    let tcp_listener = TcpListener::bind(&config.tcp_bind).await?;
    let tcp_addr = tcp_listener.local_addr()?;

    let http_listener = TcpListener::bind(&config.http_bind).await?;
    let http_addr = http_listener.local_addr()?;

    let ai_interval = config.wildlife_wander_interval_ms;
    let dir_url = config.directory_url.clone();
    let upstream_urls = config.upstream_servers_urls.clone();
    let state = ServerState::new(config, tcp_addr.port())?;
    let router = build_http_router(state.clone());

    if !upstream_urls.is_empty() {
        sync_upstream_servers_once(&state, &upstream_urls).await;
    }

    let tcp_state = state.clone();
    let tcp_handle = tokio::spawn(async move {
        if let Err(err) = run_tcp_server(tcp_listener, tcp_state).await {
            tracing::error!(%err, "TCP server stopped");
        }
    });
    let http_handle = tokio::spawn(async move {
        if let Err(err) = axum::serve(
            http_listener,
            router.into_make_service_with_connect_info::<SocketAddr>(),
        )
        .await
        {
            tracing::error!(%err, "HTTP server stopped");
        }
    });
    let ai_handle = ai_interval.map(|ms| {
        let ai_state = state.clone();
        tokio::spawn(async move {
            run_wildlife_ai_loop(ai_state, ms).await;
        })
    });
    let heartbeat_handle = dir_url.map(|url| {
        let hb_state = state.clone();
        let port = http_addr.port();
        tokio::spawn(async move {
            run_directory_heartbeat_loop(hb_state, url, port).await;
        })
    });
    let upstream_sync_handle = if !upstream_urls.is_empty() {
        let sync_state = state.clone();
        Some(tokio::spawn(async move {
            run_upstream_servers_sync_loop(sync_state, upstream_urls).await;
        }))
    } else {
        None
    };

    Ok(RunningServer {
        http_addr,
        tcp_addr,
        state,
        http_handle,
        tcp_handle,
        ai_handle,
        heartbeat_handle,
        upstream_sync_handle,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use axum::body::Body;
    use axum::http::{header, Request, StatusCode};
    use durango_wire::{
        encode_frame, pack_empty_struct, try_decode_frame, ActionsMsg, AppearAnimalMsg,
        AppearArtifactMsg, ArtifactBuiltMsg, ArtifactCompletedMsg, Auth, BattleBegunMsg,
        BattleEndedMsg, BuildArtifactMsg, BuyProductMsg, ChunkMsg, Clock, CollectMsg, CollectedMsg,
        CollectibleChangedMsg, CollectibleMsg, CompleteArtifactMsg, CraftMsg, CraftedMsg,
        DamagedMsg, DestructArtifactMsg, DestructingMsg, DisappearEntity, DisappearEntityOnTileMsg,
        EmigratedMsg, EntityDiedMsg, EntityRevivedMsg, EquipMsg, EquipmentsMsg, ExpGainedMsg,
        GetClock, GetCollectibleMsg, GetQuestsMsg, GetRegionMapInfoMsg, InventoryInfosMsg,
        InventoryMsg, InventoryOrderMsg, InventoryUpdatedMsg, LearnSkillMsg, LeaveClanMsg,
        Location, LockOrUnlockItemsMsg, MakeClanMsg, MemberMsg, MoveMsg, Movement, OccupiedMsg,
        OccupyArtifactSiteMsg, PetsInfoMsg, PlayerDisplayMsg, ProductsMsg, QuestsMsg,
        RecommendedStableRegionsMsg, RegionMapInfoMsg, RegisterProductMsg,
        ResearchSkillCategoryMsg, RestOnMsg, ReviveImmediatelyInfoMsg, ReviveImmediatelyMsg,
        ReviveMsg, RunDownMsg, RunUpMsg, SayInExclusiveChannelMsg, SearchProductsMsg,
        SelectBattleTargetMsg, SetBaseMoveSpeedMsg, SetChunk, SkillsMsg, StatisticsMsg,
        SurvivalMsg, TeleportedMsg, TimerMsg, TouchMsg, TouchedMsg, TravelToStableRegionMsg, UntrainSkillMsg, UseBattleActionMsg, UseItemMsg, WarpMsg, Welcome,
        BUILDING_STATE_COMPLETED, EQUIP_SLOT_1, INTERACTION_ATTACK, INTERACTION_BUILD_ARTIFACT,
        INTERACTION_COLLECT, INTERACTION_DESTRUCT_ARTIFACT, INTERACTION_REST, RESULT_SUCCESS,
        SKILL_CAT_GATHERING, TC_ACTIONS, TC_APPEAR_ANIMAL, TC_APPEAR_ARTIFACT, TC_APPEAR_PLAYER,
        TC_ARTIFACT_BUILT, TC_ARTIFACT_COMPLETED, TC_ATTACHABLE_ACCESSORIES, TC_AUTH,
        TC_AVAILABLE_EMOTIONS, TC_BATTLE_BEGUN, TC_BATTLE_ENDED, TC_BUILD_ARTIFACT, TC_BUY_PRODUCT,
        TC_CHEAT_FLAGS, TC_CHUNK, TC_CLOCK, TC_COLLECT, TC_COLLECTED, TC_COLLECTIBLE,
        TC_COLLECTIBLE_CHANGED, TC_COMPLETE_ARTIFACT, TC_CRAFT, TC_CRAFTED, TC_DAMAGED, TC_DASHED,
        TC_DEFOGGED_CHUNKS, TC_DESTRUCTING, TC_DESTRUCT_ARTIFACT, TC_DISAPPEAR_ENTITY,
        TC_DISAPPEAR_ENTITY_ON_TILE, TC_EMIGRATED, TC_ENTITY_DIED, TC_ENTITY_REVIVED, TC_EQUIP,
        TC_EQUIPMENTS, TC_ESTATE_LICENSES, TC_EXP_GAINED, TC_FAILED, TC_GET_ACTIONS,
        TC_GET_ATTACHABLE_ACCESSORIES, TC_GET_AVAILABLE_EMOTIONS, TC_GET_CHEAT_FLAGS, TC_GET_CLOCK,
        TC_GET_COLLECTIBLE, TC_GET_ESTATE_LICENSES, TC_GET_PETS_INFO, TC_GET_QUESTS,
        TC_GET_QUEST_CATEGORIES, TC_GET_REGION_MAP_INFO, TC_GET_RESISTANCE_EXP_CAPS,
        TC_GET_REVIVE_IMMEDIATELY_INFO, TC_GET_SKILLS, TC_GET_STATISTICS, TC_GET_WARP_COSTS,
        TC_INVENTORY, TC_INVENTORY_INFOS, TC_INVENTORY_ORDER, TC_INVENTORY_UPDATED,
        TC_IS_WARPHOLE_AVAILABLE, TC_LEARN_SKILL, TC_LEAVE_CLAN, TC_LOCK_OR_UNLOCK_ITEMS,
        TC_MAKE_CLAN, TC_MEMBER, TC_MOVE, TC_OCCUPIED, TC_OCCUPY_ARTIFACT_SITE, TC_OK, TC_PETS_INFO,
        TC_PLAYER_DISPLAY, TC_PRODUCTS, TC_QUESTS, TC_QUEST_CATEGORIES, TC_READY,
        TC_RECOMMENDED_STABLE_REGIONS, TC_RECOMMEND_STABLE_REGIONS, TC_REGION_MAP_INFO,
        TC_REGISTER_PRODUCT, TC_REPLY_SEQUENCE_MARK, TC_RESEARCH_SKILL_CATEGORY,
        TC_RESISTANCE_EXP_CAPS, TC_REST_ON, TC_REVIVE, TC_REVIVED, TC_REVIVE_IMMEDIATELY,
        TC_REVIVE_IMMEDIATELY_INFO, TC_REWARDED, TC_RUN_DOWN, TC_RUN_UP,
        TC_SAY_IN_EXCLUSIVE_CHANNEL, TC_SEARCH_PRODUCTS, TC_SELECT_BATTLE_TARGET,
        TC_SET_BASE_MOVE_SPEED, TC_SET_CHUNK, TC_SKILLS, TC_STATISTICS, TC_SURVIVAL,
        TC_TELEPORTED, TC_TIMER, TC_TOUCH, TC_TOUCHED, TC_TRAVEL_TO_STABLE_REGION,
        TC_UNTRAIN_SKILL, TC_USE_BATTLE_ACTION, TC_USE_ITEM, TC_WARP, TC_WARP_COSTS, TC_WELCOME,
        TELEPORT_TYPE_REVIVE,
    };
    use http_body_util::BodyExt;
    use serde_json::Value;
    use tokio::io::{AsyncReadExt, AsyncWriteExt};
    use tokio::net::TcpStream;
    use tower::ServiceExt;

    async fn read_one_frame(stream: &mut TcpStream, buf: &mut Vec<u8>) -> durango_wire::RawFrame {
        let mut tmp = [0u8; 4096];
        loop {
            if let Some((f, used)) = try_decode_frame(buf).unwrap() {
                buf.drain(..used);
                return f;
            }
            let n = tokio::time::timeout(
                std::time::Duration::from_secs(3),
                stream.read(&mut tmp),
            )
            .await
            .expect("timed out waiting for TCP frame")
            .unwrap();
            assert!(n > 0, "unexpected EOF waiting for frame");
            buf.extend_from_slice(&tmp[..n]);
        }
    }

    async fn read_xp_frames(
        stream: &mut TcpStream,
        buf: &mut Vec<u8>,
        cur_level: &mut i32,
    ) -> (ExpGainedMsg, StatisticsMsg, SkillsMsg, bool) {
        let f_eg = read_one_frame(stream, buf).await;
        assert_eq!(f_eg.header.type_code, TC_EXP_GAINED);
        let eg = ExpGainedMsg::unpack(&f_eg.msgpack).unwrap();
        let f_st = read_one_frame(stream, buf).await;
        assert_eq!(f_st.header.type_code, TC_STATISTICS);
        let st = StatisticsMsg::unpack(&f_st.msgpack).unwrap();
        let f_sk = read_one_frame(stream, buf).await;
        assert_eq!(f_sk.header.type_code, TC_SKILLS);
        let sk = SkillsMsg::unpack(&f_sk.msgpack).unwrap();
        let leveled = st.level > *cur_level;
        if leveled {
            let f_rew = read_one_frame(stream, buf).await;
            assert_eq!(f_rew.header.type_code, TC_REWARDED);
            *cur_level = st.level;
        }
        (eg, st, sk, leveled)
    }

    #[tokio::test]
    async fn full_v336_handshake_chunks_movement_sync_and_sqlite_persistence() {
        let _ = tracing_subscriber::fmt()
            .with_max_level(tracing::Level::DEBUG)
            .with_test_writer()
            .try_init();
        let db_file = std::env::temp_dir().join(format!(
            "durango_rs_test_{}.db",
            uuid::Uuid::new_v4().simple()
        ));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(db_file.clone()),
            collect_delay_ms: 40,
            craft_delay_ms: 40,
            build_delay_ms: 40,
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg.clone()).await.unwrap();
        let router = build_http_router(srv.state.clone());

        // 1. POST /sessions + POST /players using real BestHTTP multipart/form-data!
        let mp_sessions = "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"account_id\"\r\n\r\ntest-alpha-01\r\n--BestHTTPBoundary--\r\n";
        let r = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "multipart/form-data; boundary=BestHTTPBoundary")
                    .body(Body::from(mp_sessions))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let s1: Value = serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok1 = s1["session_token"].as_str().unwrap().to_string();

        let mp_player = concat!(
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"name\"\r\n\r\nAlphaPioneer\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"gender\"\r\n\r\nfemale\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"job\"\r\n\r\n2\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"starter_outfit\"\r\n\r\n2\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"region\"\r\n\r\npe10gr_1\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"model_info\"\r\n\r\n{\"skin_color\":\"F5C9A6\",\"hair_color\":\"7A4A28\",\"hair\":\"Models/PC/Female/Hair/f_hair_longwave\"}\r\n",
            "--BestHTTPBoundary--\r\n"
        );
        let r = router
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok1)
                    .header(header::CONTENT_TYPE, "multipart/form-data; boundary=BestHTTPBoundary")
                    .body(Body::from(mp_player))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let p1: Value = serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid1 = p1["entity_id"].as_str().unwrap().to_string();

        // Verify persisted full-color CharacterAppearance (never black/grayish!)
        let ch1 = srv.state.resolve_character(&eid1).await.unwrap();
        let app1 = srv.state.build_character_appearance(&ch1);
        assert_eq!(app1.entity_type(), 1001); // female
        assert_eq!(app1.skin_color, "F5C9A6");
        assert_eq!(app1.hair_color, "7A4A28");
        assert_eq!(app1.body, "Models/PC/Female/Body/f_body_school.FBX");

        // Verify GET /players?name=Alpha search endpoint returns FoundPlayersJson {"players": [...]}
        let sr = router
            .clone()
            .oneshot(Request::get("/players?name=Alpha").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(sr.status(), StatusCode::OK);
        let sres: Value = serde_json::from_slice(&sr.into_body().collect().await.unwrap().to_bytes()).unwrap();
        assert_eq!(sres["players"].as_array().unwrap().len(), 1);
        assert_eq!(sres["players"][0]["name"], "AlphaPioneer");

        // Verify creating a 2nd character on the same account + DELETE /players/:entity_id works cleanly
        let mp_player2 = concat!(
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"name\"\r\n\r\nAltChar\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"job\"\r\n\r\n-1\r\n",
            "--BestHTTPBoundary\r\nContent-Disposition: form-data; name=\"model_info\"\r\n\r\n{\"starter_outfit\":5}\r\n",
            "--BestHTTPBoundary--\r\n"
        );
        let r_alt = router
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok1)
                    .header(header::CONTENT_TYPE, "multipart/form-data; boundary=BestHTTPBoundary")
                    .body(Body::from(mp_player2))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r_alt.status(), StatusCode::OK);
        let alt_json: Value = serde_json::from_slice(&r_alt.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let alt_eid = alt_json["entity_id"].as_str().unwrap().to_string();
        assert_ne!(alt_eid, eid1);

        let r_del = router
            .clone()
            .oneshot(
                Request::delete(format!("/players/{alt_eid}"))
                    .header(header::AUTHORIZATION, &tok1)
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r_del.status(), StatusCode::OK);
        assert!(srv.state.db.get_by_entity_id(&alt_eid).unwrap().is_none());
        assert!(srv.state.db.get_by_entity_id(&eid1).unwrap().is_some());
        let _ = srv.state.bind_session_to_entity(&tok1, &eid1).await;

        // Verify /terrains/pe10gr_1 & combined chunk endpoint
        let tr = router
            .clone()
            .oneshot(Request::get("/terrains/pe10gr_1/4,4").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(tr.status(), StatusCode::OK);
        assert_eq!(tr.into_body().collect().await.unwrap().to_bytes().len(), 324 + 289 + 867);

        // Second player session
        let r2 = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=test-beta-02"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s2: Value = serde_json::from_slice(&r2.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid2 = s2["user_id"].as_str().unwrap().to_string();
        let tok2 = s2["session_token"].as_str().unwrap().to_string();

        // 2. TCP Handshake for Client A: GetClock -> Clock, Auth -> Welcome, Ready -> OK, SetChunk -> 9 Chunks
        let mut c1 = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut b1 = Vec::new();
        c1.write_all(&encode_frame(1.0, 1, 0, TC_GET_CLOCK, &GetClock { time: 42.5 }.pack().unwrap()).unwrap())
            .await
            .unwrap();
        c1.write_all(
            &encode_frame(
                1.0,
                2,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid1.clone(),
                    session_token: tok1.clone(),
                    client_version: "5.2.1".into(),
                    device_model: "RustTest".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();

        let f_clock = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_clock.header.type_code, TC_CLOCK);
        assert_eq!(Clock::unpack(&f_clock.msgpack).unwrap().client_time, 42.5);

        let f_welcome = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_welcome.header.type_code, TC_WELCOME);
        let w = Welcome::unpack(&f_welcome.msgpack).unwrap();
        assert_eq!(w.name, "AlphaPioneer");
        assert_eq!(w.region.terrain_id, "pe10gr_1");

        c1.write_all(&encode_frame(1.0, 3, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_ok = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_ok.header.type_code, TC_OK);
        assert_eq!(f_ok.header.reply_of, 3);
        let f_init_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_init_surv.header.type_code, TC_SURVIVAL);
        let f_stats = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_stats.header.type_code, TC_STATISTICS);
        assert_eq!(StatisticsMsg::unpack(&f_stats.msgpack).unwrap().level, 1);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_SKILLS);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_POINTS);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_PIONEER_GRADE_INFO);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_ESTATE_LICENSES);
        let f_inv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_inv.header.type_code, TC_INVENTORY);
        let init_inv = InventoryMsg::unpack(&f_inv.msgpack).unwrap();
        assert_eq!(init_inv.entity_id, eid1);
        assert_eq!(init_inv.items.len(), 1);
        assert_eq!(init_inv.items[0].prototype, "clothes_student");
        let f_equip = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_equip.header.type_code, TC_EQUIPMENTS);
        assert_eq!(
            EquipmentsMsg::unpack(&f_equip.msgpack).unwrap().slots["body"].prototype,
            "clothes_student"
        );
        let f_defog = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_defog.header.type_code, TC_DEFOGGED_CHUNKS);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_QUEST_CATEGORIES);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_CHEAT_FLAGS);
        let f_self_appear = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_self_appear.header.type_code, TC_APPEAR_PLAYER);
        // Harbor Dock POI (7001) seeded from pois.yml / entry port streams via AppearArtifact
        let f_dock = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dock.header.type_code, TC_APPEAR_ARTIFACT);
        assert_eq!(AppearArtifactMsg::unpack(&f_dock.msgpack).unwrap().entity_type, 7001);
        // Step 7: 3 regional wildlife dinosaurs stream via AppearAnimal (91)
        let mut first_dino_id = String::new();
        for idx in 0..3 {
            let f_ani = read_one_frame(&mut c1, &mut b1).await;
            assert_eq!(f_ani.header.type_code, TC_APPEAR_ANIMAL);
            let ani = AppearAnimalMsg::unpack(&f_ani.msgpack).unwrap();
            assert!(ani.is_alive);
            assert_eq!(ani.motion_option, 1); // LOOPING stand animation
            assert!(ani.motion_name.ends_with("_Stand"));
            if idx == 0 {
                first_dino_id = ani.entity_id;
            }
        }
        assert!(!first_dino_id.is_empty());

        c1.write_all(&encode_frame(1.0, 4, 0, TC_SET_CHUNK, &SetChunk { x: 4, y: 4 }.pack().unwrap()).unwrap())
            .await
            .unwrap();
        let mut saw_fallback_node = false;
        for _ in 0..9 {
            let fc = read_one_frame(&mut c1, &mut b1).await;
            assert_eq!(fc.header.type_code, TC_CHUNK);
            let ch = ChunkMsg::unpack(&fc.msgpack).unwrap();
            if ch.x == 4 && ch.y == 4 && !ch.garden.is_empty() {
                saw_fallback_node = true;
            }
        }
        assert!(saw_fallback_node, "expected chunk (4,4) to include natural node");

        // Stepping to adjacent chunk (5,4) must ONLY stream the 3 newly entered edge chunks (6,3..5),
        // NOT re-send the 6 overlapping chunks already rendered on screen!
        c1.write_all(&encode_frame(1.05, 401, 0, TC_SET_CHUNK, &SetChunk { x: 5, y: 4 }.pack().unwrap()).unwrap())
            .await
            .unwrap();
        let mut new_coords = Vec::new();
        for _ in 0..3 {
            let fc = read_one_frame(&mut c1, &mut b1).await;
            assert_eq!(fc.header.type_code, TC_CHUNK);
            let ch = ChunkMsg::unpack(&fc.msgpack).unwrap();
            new_coords.push((ch.x, ch.y));
        }
        assert_eq!(new_coords, vec![(6, 3), (6, 4), (6, 5)]);
        // Step back to (4,4) -> only streams (3,3..5)
        c1.write_all(&encode_frame(1.06, 402, 0, TC_SET_CHUNK, &SetChunk { x: 4, y: 4 }.pack().unwrap()).unwrap())
            .await
            .unwrap();
        let mut back_coords = Vec::new();
        for _ in 0..3 {
            let fc = read_one_frame(&mut c1, &mut b1).await;
            assert_eq!(fc.header.type_code, TC_CHUNK);
            let ch = ChunkMsg::unpack(&fc.msgpack).unwrap();
            back_coords.push((ch.x, ch.y));
        }
        assert_eq!(back_coords, vec![(3, 3), (3, 4), (3, 5)]);

        // 3. Connect Client B, verify mutual AppearPlayer, Move broadcast A -> B
        let mut c2 = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut b2 = Vec::new();
        c2.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid2.clone(),
                    session_token: tok2,
                    client_version: "5.2.1".into(),
                    device_model: "RustTest".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let _ = read_one_frame(&mut c2, &mut b2).await; // Welcome
        c2.write_all(&encode_frame(1.0, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_SURVIVAL);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_STATISTICS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_SKILLS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_POINTS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_PIONEER_GRADE_INFO);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_ESTATE_LICENSES);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_INVENTORY);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_EQUIPMENTS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_DEFOGGED_CHUNKS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_QUEST_CATEGORIES);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_CHEAT_FLAGS);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_APPEAR_PLAYER); // self B
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_APPEAR_PLAYER); // peer A
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_APPEAR_ARTIFACT); // Harbor dock POI
        for _ in 0..3 {
            assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_APPEAR_ANIMAL);
        }
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_APPEAR_PLAYER); // A sees B

        // 4. Step 4 Survival Slice: Touch -> Touched -> Collect -> Timer + Collected + DisappearEntityOnTile (both A & B)
        c1.write_all(
            &encode_frame(
                2.0,
                5,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    entity_type: 16048,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_touched = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_touched.header.type_code, TC_TOUCHED);
        assert_eq!(f_touched.header.reply_of, 5);
        let touched = TouchedMsg::unpack(&f_touched.msgpack).unwrap();
        assert!(!touched.collectible.generators.is_empty());
        for g in &touched.collectible.generators {
            assert_eq!(
                g.tool_requirements.get("bare_hands"),
                Some(&1),
                "generator {} must include bare_hands so Unity ReservationQueue allows stacking",
                g.id
            );
        }
        let total_units: i32 = touched.collectible.generators.iter().map(|g| g.amount).sum();
        assert!(total_units >= 2, "natural tile should yield multiple resources");
        let first_gen_id = touched.collectible.generators[0].id.clone();
        let first_gen_initial = touched.collectible.generators[0].amount;

        // Gather 1st unit -> ran_out == false (tree/bush stays standing!)
        c1.write_all(
            &encode_frame(
                2.1,
                6,
                0,
                TC_COLLECT,
                &CollectMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    generator_id: first_gen_id.clone(),
                    level: 1,
                    tool_item_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_open = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_open.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(f_open.header.reply_of, 6);

        let f_timer = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_timer.header.type_code, TC_TIMER);
        assert_eq!(f_timer.header.reply_of, 6);
        assert!(TimerMsg::unpack(&f_timer.msgpack).unwrap().duration > 0.0);

        let f_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_surv.header.type_code, TC_SURVIVAL);
        let surv_after_gather = SurvivalMsg::unpack(&f_surv.msgpack).unwrap();
        assert!(surv_after_gather.gauges["energy"].nodes[0].1 < 100.0);

        let f_inv_upd = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_inv_upd.header.type_code, TC_INVENTORY_UPDATED);
        let upd_after_gather = InventoryUpdatedMsg::unpack(&f_inv_upd.msgpack).unwrap();
        assert_eq!(upd_after_gather.items.len(), 1);
        assert!(
            !upd_after_gather.items[0].tags.is_empty(),
            "gathered items must carry prototype tags for crafting/tool matching"
        );

        let f_collected = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_collected.header.type_code, TC_COLLECTED);
        assert_eq!(f_collected.header.reply_of, 6);
        let collected = CollectedMsg::unpack(&f_collected.msgpack).unwrap();
        assert_eq!(collected.result, RESULT_SUCCESS);
        assert!(!collected.ran_out, "resource must NOT deplete after gathering only 1 item");
        assert_eq!(collected.items.len(), 1);
        let gathered_item_id = collected.items[0].id.clone();

        let f_close = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_close.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(f_close.header.reply_of, 6);

        // Server broadcasts CollectibleChanged right after non-depleting Collect so UI updates 3 -> 2 immediately!
        let f_cchg = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_cchg.header.type_code, TC_COLLECTIBLE_CHANGED);
        let _ = CollectibleChangedMsg::unpack(&f_cchg.msgpack).unwrap();
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_COLLECTIBLE_CHANGED);
        let mut p1_level = 1i32;
        let (eg1, _, _, _) = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
        assert!(eg1.exp > 0, "expected positive gather XP");

        // Client replies with GetCollectible -> server returns Collectible with decremented amount!
        c1.write_all(
            &encode_frame(
                2.11,
                600,
                0,
                TC_GET_COLLECTIBLE,
                &GetCollectibleMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_col = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_col.header.type_code, TC_COLLECTIBLE);
        assert_eq!(f_col.header.reply_of, 600);
        let col_msg = CollectibleMsg::unpack(&f_col.msgpack).unwrap();
        assert_eq!(
            col_msg
                .generators
                .iter()
                .find(|g| g.id == first_gen_id)
                .unwrap()
                .amount,
            first_gen_initial - 1
        );

        // Re-touch tile -> verify remaining count decremented by 1 and tree is still gatherable!
        c1.write_all(
            &encode_frame(
                2.12,
                601,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    entity_type: 16048,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let retouched = TouchedMsg::unpack(&read_one_frame(&mut c1, &mut b1).await.msgpack).unwrap();
        let rem_first = retouched
            .collectible
            .generators
            .iter()
            .find(|g| g.id == first_gen_id)
            .unwrap()
            .amount;
        assert_eq!(rem_first, first_gen_initial - 1);

        // Drain the remaining units on tile (64, 71) -> final collect sets ran_out == true and broadcasts DisappearEntityOnTile!
        let mut drain_seq = 602u32;
        for step in 1..total_units {
            let gid = retouched
                .collectible
                .generators
                .iter()
                .flat_map(|g| std::iter::repeat_n(g.id.clone(), g.amount as usize))
                .nth((step - 1) as usize)
                .unwrap_or_else(|| first_gen_id.clone());
            c1.write_all(
                &encode_frame(
                    2.13,
                    drain_seq,
                    0,
                    TC_COLLECT,
                    &CollectMsg {
                        entity_id: String::new(),
                        tile_x: 64,
                        tile_y: 71,
                        generator_id: gid,
                        level: 1,
                        tool_item_id: String::new(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            drain_seq += 1;
            assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
            assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_TIMER);
            assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_SURVIVAL);
            assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_INVENTORY_UPDATED);
            let fc = read_one_frame(&mut c1, &mut b1).await;
            assert_eq!(fc.header.type_code, TC_COLLECTED);
            let cm = CollectedMsg::unpack(&fc.msgpack).unwrap();
            assert_eq!(cm.ran_out, step == total_units - 1);
            assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
            if !cm.ran_out {
                assert_eq!(
                    read_one_frame(&mut c1, &mut b1).await.header.type_code,
                    TC_COLLECTIBLE_CHANGED
                );
                assert_eq!(
                    read_one_frame(&mut c2, &mut b2).await.header.type_code,
                    TC_COLLECTIBLE_CHANGED
                );
                let _ = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
            }
        }

        let f_dis_tile_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dis_tile_a.header.type_code, TC_DISAPPEAR_ENTITY_ON_TILE);
        let dt_a = DisappearEntityOnTileMsg::unpack(&f_dis_tile_a.msgpack).unwrap();
        assert_eq!((dt_a.tile_x, dt_a.tile_y), (64, 71));
        let _ = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;

        let f_dis_tile_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_dis_tile_b.header.type_code, TC_DISAPPEAR_ENTITY_ON_TILE);
        let dt_b = DisappearEntityOnTileMsg::unpack(&f_dis_tile_b.msgpack).unwrap();
        assert_eq!((dt_b.tile_x, dt_b.tile_y), (64, 71));

        // 5. Lock gathered item -> InventoryInfos, Dash -> stamina Survival, Area Chat -> both A & B
        c1.write_all(
            &encode_frame(
                2.2,
                7,
                0,
                TC_LOCK_OR_UNLOCK_ITEMS,
                &LockOrUnlockItemsMsg {
                    lock: true,
                    item_ids: vec![gathered_item_id.clone()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_infos = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_infos.header.type_code, TC_INVENTORY_INFOS);
        let infos = InventoryInfosMsg::unpack(&f_infos.msgpack).unwrap();
        assert_eq!(infos.locked_item_ids, vec![gathered_item_id.clone()]);

        c1.write_all(&encode_frame(2.3, 8, 0, TC_DASHED, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_dash_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dash_surv.header.type_code, TC_SURVIVAL);
        let dash_surv = SurvivalMsg::unpack(&f_dash_surv.msgpack).unwrap();
        assert!(dash_surv.gauges["stamina"].nodes[0].1 <= 80.5);
        assert_eq!(dash_surv.gauges["stamina"].nodes.len(), 2);

        c1.write_all(
            &encode_frame(
                2.4,
                9,
                0,
                TC_SAY_IN_EXCLUSIVE_CHANNEL,
                &SayInExclusiveChannelMsg {
                    entity_id: eid1.clone(),
                    time: 2.4,
                    text: "Hello Durango!".into(),
                    speaker_name: None,
                    speaker_freq: 0,
                    channel_type: 0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_chat_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_chat_a.header.type_code, TC_SAY_IN_EXCLUSIVE_CHANNEL);
        let chat_a = SayInExclusiveChannelMsg::unpack(&f_chat_a.msgpack).unwrap();
        assert_eq!(chat_a.text, "Hello Durango!");
        assert_eq!(chat_a.speaker_name.as_deref(), Some("AlphaPioneer"));
        let f_chat_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_chat_b.header.type_code, TC_SAY_IN_EXCLUSIVE_CHANNEL);
        assert_eq!(
            SayInExclusiveChannelMsg::unpack(&f_chat_b.msgpack).unwrap().text,
            "Hello Durango!"
        );

        // 5b. Step 5: Unlock gathered item -> Craft stone_knife -> Equip weapon -> UseItem berry -> InventoryOrder
        c1.write_all(
            &encode_frame(
                2.45,
                11,
                0,
                TC_LOCK_OR_UNLOCK_ITEMS,
                &LockOrUnlockItemsMsg {
                    lock: false,
                    item_ids: vec![gathered_item_id.clone()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_unlock = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_unlock.header.type_code, TC_INVENTORY_INFOS);
        assert!(InventoryInfosMsg::unpack(&f_unlock.msgpack).unwrap().locked_item_ids.is_empty());

        // Craft stone_knife consuming gathered_item_id
        let mut craft_mats = std::collections::BTreeMap::new();
        craft_mats.insert("material".into(), vec![gathered_item_id.clone()]);
        c1.write_all(
            &encode_frame(
                2.5,
                12,
                0,
                TC_CRAFT,
                &CraftMsg {
                    recipe_id: "stone_knife".into(),
                    materials: craft_mats,
                    tool_item_id: None,
                    reform_slot_index: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_craft_open = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_craft_open.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(f_craft_open.header.reply_of, 12);
        let f_craft_timer = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_craft_timer.header.type_code, TC_TIMER);
        let f_craft_upd = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_craft_upd.header.type_code, TC_INVENTORY_UPDATED);
        let craft_upd = InventoryUpdatedMsg::unpack(&f_craft_upd.msgpack).unwrap();
        assert_eq!(craft_upd.removed_item_ids, vec![gathered_item_id.clone()]);
        assert_eq!(craft_upd.items.len(), 1);
        let crafted_knife_id = craft_upd.items[0].id.clone();
        assert_eq!(craft_upd.items[0].prototype, "tool_stone_knife");

        let f_craft_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_craft_surv.header.type_code, TC_SURVIVAL);
        let f_crafted = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_crafted.header.type_code, TC_CRAFTED);
        assert_eq!(f_crafted.header.reply_of, 12);
        let crafted_msg = CraftedMsg::unpack(&f_crafted.msgpack).unwrap();
        assert_eq!(crafted_msg.result, RESULT_SUCCESS);
        assert_eq!(crafted_msg.items[0].id, crafted_knife_id);
        let f_craft_close = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_craft_close.header.type_code, TC_REPLY_SEQUENCE_MARK);
        let (eg_craft, _, _, _) = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
        assert!(eg_craft.exp > 0, "expected positive craft XP");

        // Equip crafted knife -> Equipments reply + PlayerDisplay broadcast to both A & B
        c1.write_all(
            &encode_frame(
                2.6,
                13,
                0,
                TC_EQUIP,
                &EquipMsg {
                    slot_name: "weapon".into(),
                    slot_type: EQUIP_SLOT_1,
                    item_id: crafted_knife_id.clone(),
                    action: "equip".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_eq = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_eq.header.type_code, TC_EQUIPMENTS);
        assert_eq!(f_eq.header.reply_of, 13);
        let eq_msg = EquipmentsMsg::unpack(&f_eq.msgpack).unwrap();
        assert_eq!(eq_msg.slots["weapon"].id, crafted_knife_id);
        let f_disp_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_disp_a.header.type_code, TC_PLAYER_DISPLAY);
        let f_act_eq = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_act_eq.header.type_code, TC_ACTIONS);
        let f_disp_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_disp_b.header.type_code, TC_PLAYER_DISPLAY);
        assert_eq!(
            PlayerDisplayMsg::unpack(&f_disp_b.msgpack).unwrap().equip.as_deref(),
            Some("clothes_student")
        );

        // Seed a berry consumable in DB, eat it via UseItem -> InventoryUpdated + Survival + OK
        let berry_id = "berry-consumable-1".to_string();
        srv.state
            .db
            .insert_items(&[crate::db::ItemRecord {
                item_id: berry_id.clone(),
                entity_id: eid1.clone(),
                prototype_id: "fruit_berry".into(),
                name: "Wild Berry".into(),
                icon: "tag_material_fruit".into(),
                level: 1,
                size: 1,
                durability: 100.0,
                max_durability: 100.0,
                locked: false,
                collectible_id: None,
                generator_id: None,
                created_at: 2.65,
            }])
            .unwrap();
        c1.write_all(
            &encode_frame(
                2.7,
                14,
                0,
                TC_USE_ITEM,
                &UseItemMsg {
                    item_id: berry_id.clone(),
                    accept: true,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_eat_upd = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_eat_upd.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(
            InventoryUpdatedMsg::unpack(&f_eat_upd.msgpack).unwrap().removed_item_ids,
            vec![berry_id]
        );
        let f_eat_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_eat_surv.header.type_code, TC_SURVIVAL);
        assert!(
            SurvivalMsg::unpack(&f_eat_surv.msgpack).unwrap().gauges["energy"].nodes[0].1 >= 90.0
        );
        let f_eat_ok = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_eat_ok.header.type_code, TC_OK);
        assert_eq!(f_eat_ok.header.reply_of, 14);

        // InventoryOrder persists cleanly
        c1.write_all(
            &encode_frame(
                2.75,
                15,
                0,
                TC_INVENTORY_ORDER,
                &InventoryOrderMsg {
                    is_artifact_target: false,
                    item_order: vec![crafted_knife_id.clone()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();

        let mv = MoveMsg {
            entity_id: eid1.clone(),
            movements: vec![Movement {
                motion_name: "run".into(),
                motion_option: 0,
                playback_rate: 1.0,
                rot_speed: 6.0,
                path: vec![Location {
                    x: 12650.0,
                    y: 14250.0,
                    yaw: 0.5,
                    time: 10.0,
                    floor: 0,
                    height: 0.0,
                }],
            }],
        };
        c1.write_all(&encode_frame(2.8, 16, 0, TC_MOVE, &mv.pack().unwrap()).unwrap())
            .await
            .unwrap();
        let f_mv = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_mv.header.type_code, TC_MOVE);
        let decoded_mv = MoveMsg::unpack(&f_mv.msgpack).unwrap();
        assert_eq!(decoded_mv.entity_id, eid1);
        assert_eq!(decoded_mv.movements[0].path[0].x, 12650.0);

        // 5c. Step 6: World Building & Artifacts (Occupy -> Touch -> Build -> Complete -> RestOn -> Persist -> Destruct)
        c1.write_all(
            &encode_frame(
                3.0,
                17,
                0,
                TC_OCCUPY_ARTIFACT_SITE,
                &OccupyArtifactSiteMsg {
                    blueprint_id: Some("tent".into()),
                    item_id: None,
                    tile_x: 68,
                    tile_y: 72,
                    floor: None,
                    size_x: 2,
                    size_y: 2,
                    stories: None,
                    rotation: 0,
                    modular_entity_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_app_art_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_app_art_a.header.type_code, TC_APPEAR_ARTIFACT);
        let app_art_a = AppearArtifactMsg::unpack(&f_app_art_a.msgpack).unwrap();
        let tent_id = app_art_a.entity_id.clone();
        assert_eq!((app_art_a.tile_x, app_art_a.tile_y), (68, 72));
        let f_app_art_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_app_art_b.header.type_code, TC_APPEAR_ARTIFACT);
        assert_eq!(AppearArtifactMsg::unpack(&f_app_art_b.msgpack).unwrap().entity_id, tent_id);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_TIMER);
        let f_occ = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_occ.header.type_code, TC_OCCUPIED);
        assert_eq!(OccupiedMsg::unpack(&f_occ.msgpack).unwrap().entity_id, tent_id);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);

        // Touch occupied tent -> BuildArtifact (101) + DestructArtifact (103)
        c1.write_all(
            &encode_frame(
                3.1,
                18,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                    entity_type: 6151,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_t_occ = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_t_occ.header.type_code, TC_TOUCHED);
        let t_occ = TouchedMsg::unpack(&f_t_occ.msgpack).unwrap();
        assert!(t_occ.interactions.contains(&INTERACTION_BUILD_ARTIFACT));
        assert!(t_occ.interactions.contains(&INTERACTION_DESTRUCT_ARTIFACT));

        // BuildArtifact -> Timer -> ArtifactBuilt (both A & B)
        c1.write_all(
            &encode_frame(
                3.2,
                19,
                0,
                TC_BUILD_ARTIFACT,
                &BuildArtifactMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                    tool_item_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_TIMER);
        let f_built_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_built_a.header.type_code, TC_ARTIFACT_BUILT);
        assert_eq!(ArtifactBuiltMsg::unpack(&f_built_a.msgpack).unwrap().entity_id, tent_id);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        let f_built_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_built_b.header.type_code, TC_ARTIFACT_BUILT);

        // CompleteArtifact -> ArtifactCompleted (both A & B) + OK
        c1.write_all(
            &encode_frame(
                3.3,
                20,
                0,
                TC_COMPLETE_ARTIFACT,
                &CompleteArtifactMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_ARTIFACT_DISPLAY);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, durango_wire::TC_ARTIFACT_STATE);
        let f_comp_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_comp_a.header.type_code, TC_ARTIFACT_COMPLETED);
        assert_eq!(ArtifactCompletedMsg::unpack(&f_comp_a.msgpack).unwrap().entity_id, tent_id);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);
        let (eg_build, _, _, _) = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
        assert!(eg_build.exp > 0, "expected positive build XP");
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_ARTIFACT_DISPLAY);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, durango_wire::TC_ARTIFACT_STATE);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_ARTIFACT_COMPLETED);

        // Touch completed tent -> Rest (407) + DestructArtifact (103), then RestOn -> OK + Survival(fatigue=0, energy=100)
        c1.write_all(
            &encode_frame(
                3.4,
                21,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                    entity_type: 6151,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let t_comp = TouchedMsg::unpack(&read_one_frame(&mut c1, &mut b1).await.msgpack).unwrap();
        assert!(t_comp.interactions.contains(&INTERACTION_REST));

        c1.write_all(
            &encode_frame(
                3.5,
                22,
                0,
                TC_REST_ON,
                &RestOnMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);
        let f_rest_surv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_rest_surv.header.type_code, TC_SURVIVAL);
        let rest_surv = SurvivalMsg::unpack(&f_rest_surv.msgpack).unwrap();
        // Continuous resting sends a downward fatigue ramp (ending at 0.0) and upward energy ramp (ending at 100.0)
        assert_eq!(rest_surv.gauges["fatigue"].nodes.last().unwrap().1, 0.0);
        assert_eq!(rest_surv.gauges["energy"].nodes.last().unwrap().1, 100.0);
        let f_fv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_fv.header.type_code, durango_wire::TC_FATIGUE_VELOCITIES);
        let fv = durango_wire::FatigueVelocitiesMsg::unpack(&f_fv.msgpack).unwrap();
        assert!((fv.velocities[&0] - (-2.0)).abs() < 0.01);
        assert_eq!(
            read_one_frame(&mut c1, &mut b1).await.header.type_code,
            durango_wire::TC_STATUS_EFFECTS
        );

        // 5d. Step 7: Wildlife Combat & Carcass Butchering (GetActions -> Touch -> Attack x2 -> EntityDied -> Butcher -> Meat persisted)
        c1.write_all(
            &encode_frame(3.6, 23, 0, TC_GET_ACTIONS, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        let f_acts = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_acts.header.type_code, TC_ACTIONS);
        let acts = ActionsMsg::unpack(&f_acts.msgpack).unwrap();
        assert!(acts.battle_actions.iter().any(|a| a.id == "onehand_default_a"));
        assert!(acts.battle_actions.iter().any(|a| a.id == "onehand_dodge"));

        // Touch alive dinosaur -> INTERACTION_ATTACK (1)
        c1.write_all(
            &encode_frame(
                3.65,
                24,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: first_dino_id.clone(),
                    tile_x: 66,
                    tile_y: 72,
                    entity_type: 2048,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let t_dino = TouchedMsg::unpack(&read_one_frame(&mut c1, &mut b1).await.msgpack).unwrap();
        assert!(t_dino.interactions.contains(&INTERACTION_ATTACK));

        // SelectBattleTarget -> Actions push
        c1.write_all(
            &encode_frame(
                3.7,
                25,
                0,
                TC_SELECT_BATTLE_TARGET,
                &SelectBattleTargetMsg {
                    entity_id: first_dino_id.clone(),
                    tile: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_ACTIONS);

        // Position Player A within melee range of first_dino_id before attacking
        {
            let (ax, ay) = {
                let animals = srv.state.animals.read().await;
                let a = &animals[&first_dino_id];
                (a.pos_x, a.pos_y)
            };
            if let Some(c) = srv.state.live_clients.write().await.get_mut(&eid1) {
                c.pos_x = ax + 100.0;
                c.pos_y = ay;
            }
        }
        // UseBattleAction onehand_default_a (45 + 15 weapon bonus = 60 dmg -> kills 60 HP Iguanodon!)
        c1.write_all(
            &encode_frame(
                3.75,
                26,
                0,
                TC_USE_BATTLE_ACTION,
                &UseBattleActionMsg {
                    action_id: "onehand_default_a".into(),
                    start_at: 3.75,
                    target_entity_id: Some(first_dino_id.clone()),
                    target_tile: None,
                    move_cancel_mode: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_stop_rest = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_stop_rest.header.type_code, TC_SURVIVAL); // entering combat stops resting
        let stop_rest_surv = SurvivalMsg::unpack(&f_stop_rest.msgpack).unwrap();
        assert_eq!(stop_rest_surv.gauges["fatigue"].nodes.len(), 1);
        let f_stop_fv = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_stop_fv.header.type_code, durango_wire::TC_FATIGUE_VELOCITIES);
        assert_eq!(
            durango_wire::FatigueVelocitiesMsg::unpack(&f_stop_fv.msgpack)
                .unwrap()
                .velocities[&0],
            0.0
        );
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_INVENTORY_UPDATED); // weapon durability wear
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_SURVIVAL); // player stamina
        let f_bb_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_bb_a.header.type_code, TC_BATTLE_BEGUN);
        assert_eq!(BattleBegunMsg::unpack(&f_bb_a.msgpack).unwrap().enemy_id, first_dino_id);
        let f_dmg_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dmg_a.header.type_code, TC_DAMAGED);
        assert_eq!(DamagedMsg::unpack(&f_dmg_a.msgpack).unwrap().damage.value, 60);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_SURVIVAL); // dino HP=0
        let f_dead_mv_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dead_mv_a.header.type_code, TC_MOVE); // dead collapse animation!
        let dead_mv_a = MoveMsg::unpack(&f_dead_mv_a.msgpack).unwrap();
        assert!(dead_mv_a.movements[0].motion_name.ends_with("_Die"));
        assert_eq!(dead_mv_a.movements[0].motion_option, 0);
        let f_dead_ci_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_dead_ci_a.header.type_code, durango_wire::TC_COMBAT_INTERACTION);
        let f_died_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_died_a.header.type_code, TC_ENTITY_DIED);
        assert_eq!(EntityDiedMsg::unpack(&f_died_a.msgpack).unwrap().entity_id, first_dino_id);
        let f_be_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_be_a.header.type_code, TC_BATTLE_ENDED);
        assert_eq!(BattleEndedMsg::unpack(&f_be_a.msgpack).unwrap().entity_id, eid1);
        let (eg_kill, _, _, _) = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
        assert!(eg_kill.exp > 0, "expected positive kill XP");
        assert!(eg_kill.bonus_exp > 0, "expected Well-Rested +25% bonus XP after resting on tent");

        // Peer B receives regional combat visuals (Damaged, dino Survival, dead Move, CombatInteraction(Dead), EntityDied)
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_DAMAGED);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_SURVIVAL);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_MOVE);
        assert_eq!(
            read_one_frame(&mut c2, &mut b2).await.header.type_code,
            durango_wire::TC_COMBAT_INTERACTION
        );
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_ENTITY_DIED);

        // Touch slain carcass -> INTERACTION_COLLECT (506) + butcher generators (meat, leather, bone)
        c1.write_all(
            &encode_frame(
                3.8,
                27,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: first_dino_id.clone(),
                    tile_x: 66,
                    tile_y: 72,
                    entity_type: 2048,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let t_carcass = TouchedMsg::unpack(&read_one_frame(&mut c1, &mut b1).await.msgpack).unwrap();
        assert!(t_carcass.interactions.contains(&INTERACTION_COLLECT));
        assert!(t_carcass.collectible.generators.iter().any(|g| g.id == "meat"));

        // Collect carcass -> Survival + ReplySequenceMark + Timer + Collected(meat) + ReplySequenceMark + DisappearEntity (both A & B)
        c1.write_all(
            &encode_frame(
                3.85,
                28,
                0,
                TC_COLLECT,
                &CollectMsg {
                    entity_id: first_dino_id.clone(),
                    tile_x: 0,
                    tile_y: 0,
                    generator_id: "meat".into(),
                    level: 2,
                    tool_item_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_TIMER);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_SURVIVAL);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_INVENTORY_UPDATED);
        let f_butcher = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_butcher.header.type_code, TC_COLLECTED);
        let butcher_res = CollectedMsg::unpack(&f_butcher.msgpack).unwrap();
        assert_eq!(butcher_res.items[0].prototype, "meat");
        assert!(!butcher_res.ran_out, "carcass should still have remaining meat/leather/bone");
        let butchered_meat_id = butcher_res.items[0].id.clone();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_COLLECTIBLE_CHANGED);
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_COLLECTIBLE_CHANGED);
        let (eg_butcher, _, _, _) = read_xp_frames(&mut c1, &mut b1, &mut p1_level).await;
        assert!(eg_butcher.exp > 0, "expected positive butcher XP");
        assert!(p1_level >= 2, "expected player to level up after gathering, crafting, building & hunting");

        // 5e. Step 9: Island Map/Warp & Player Market (List meat -> Search -> Client B Buys!)
        c1.write_all(
            &encode_frame(
                3.9,
                29,
                0,
                TC_GET_REGION_MAP_INFO,
                &GetRegionMapInfoMsg {
                    region_id: "pe10gr_1".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_rmi = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_rmi.header.type_code, TC_REGION_MAP_INFO);
        let rmi = RegionMapInfoMsg::unpack(&f_rmi.msgpack).unwrap();
        assert_eq!(rmi.region_id, "pe10gr_1");
        assert_eq!((rmi.tile_count_x, rmi.tile_count_y), (256, 256));

        c1.write_all(
            &encode_frame(
                3.92,
                30,
                0,
                TC_IS_WARPHOLE_AVAILABLE,
                &durango_wire::EntityTileMsg {
                    entity_id: String::new(),
                    tile_x: 63,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);

        c1.write_all(
            &encode_frame(3.93, 31, 0, TC_GET_WARP_COSTS, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_WARP_COSTS);

        c1.write_all(
            &encode_frame(3.94, 32, 0, TC_WARP, &WarpMsg { tile_x: 70, tile_y: 80 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let f_warp_timer = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_warp_timer.header.type_code, TC_TIMER);
        assert_eq!(f_warp_timer.header.reply_of, 32);
        let f_warp_tp = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_warp_tp.header.type_code, TC_TELEPORTED);
        assert_eq!(f_warp_tp.header.reply_of, 0);
        let tp_msg = TeleportedMsg::unpack(&f_warp_tp.msgpack).unwrap();
        assert_eq!((tp_msg.tile_x, tp_msg.tile_y), (70, 80));
        assert_eq!(tp_msg.teleport_type, durango_wire::TELEPORT_TYPE_WARP);
        // Peer C2 receives TC_MOVE broadcast when C1 warps within the same region
        let f_peer_warp_mv = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_peer_warp_mv.header.type_code, TC_MOVE);
        let peer_warp_mv = MoveMsg::unpack(&f_peer_warp_mv.msgpack).unwrap();
        assert_eq!(peer_warp_mv.entity_id, eid1);

        // Player A lists butchered meat on Market -> Player B searches & buys it!
        c1.write_all(
            &encode_frame(
                3.95,
                33,
                0,
                TC_REGISTER_PRODUCT,
                &RegisterProductMsg {
                    item_id: butchered_meat_id.clone(),
                    price: 50,
                    duration: 24.0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);

        c2.write_all(
            &encode_frame(
                3.96,
                20,
                0,
                TC_SEARCH_PRODUCTS,
                &SearchProductsMsg {
                    item_name: Some("meat".into()),
                    prototype_id: None,
                    skip: 0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_prods = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_prods.header.type_code, TC_PRODUCTS);
        let prods = ProductsMsg::unpack(&f_prods.msgpack).unwrap();
        assert_eq!(prods.products.len(), 1);
        let prod_id = prods.products[0].id.clone();

        c2.write_all(
            &encode_frame(
                3.97,
                21,
                0,
                TC_BUY_PRODUCT,
                &BuyProductMsg {
                    product_id: prod_id,
                    payment_ids: vec![],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_buy_upd = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_buy_upd.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(
            InventoryUpdatedMsg::unpack(&f_buy_upd.msgpack).unwrap().items[0].id,
            butchered_meat_id
        );
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_OK);
        assert_eq!(
            read_one_frame(&mut c2, &mut b2).await.header.type_code,
            durango_wire::TC_WALLET_UPDATED
        );

        // 5f. Step 10: Clans (MakeClan -> Member broadcast -> LeaveClan) & Multi-Island Travel
        c1.write_all(
            &encode_frame(
                3.98,
                34,
                0,
                TC_MAKE_CLAN,
                &MakeClanMsg {
                    clan_name: "RustPioneers".into(),
                    currency: 0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);
        let f_mem_a = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_mem_a.header.type_code, TC_MEMBER);
        let mem_a = MemberMsg::unpack(&f_mem_a.msgpack).unwrap();
        assert_eq!(mem_a.clan_name, "RustPioneers");
        let f_mem_b = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_mem_b.header.type_code, TC_MEMBER);
        assert_eq!(MemberMsg::unpack(&f_mem_b.msgpack).unwrap().clan_name, "RustPioneers");

        c1.write_all(
            &encode_frame(
                3.99,
                35,
                0,
                TC_RECOMMEND_STABLE_REGIONS,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_rec = read_one_frame(&mut c1, &mut b1).await;
        assert_eq!(f_rec.header.type_code, TC_RECOMMENDED_STABLE_REGIONS);
        let rec_regions = RecommendedStableRegionsMsg::unpack(&f_rec.msgpack).unwrap();
        assert!(rec_regions.region_ids.contains(&"tamed-tropical-lv10-01".to_string()));

        // LeaveClan -> cleared Member broadcast
        c1.write_all(
            &encode_frame(
                4.0,
                36,
                0,
                TC_LEAVE_CLAN,
                &LeaveClanMsg {
                    clan_id: mem_a.clan_id,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_OK);
        assert_eq!(
            MemberMsg::unpack(&read_one_frame(&mut c1, &mut b1).await.msgpack)
                .unwrap()
                .clan_name,
            ""
        );
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_MEMBER);

        drop(c1);
        let f_dis = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_dis.header.type_code, TC_DISAPPEAR_ENTITY);
        assert_eq!(DisappearEntity::unpack(&f_dis.msgpack).unwrap().entity_id, eid1);

        // TravelToStableRegion -> OK + Emigrated (2099) + DB region switches to tamed-tropical-lv10-01!
        c2.write_all(
            &encode_frame(
                4.01,
                22,
                0,
                TC_TRAVEL_TO_STABLE_REGION,
                &TravelToStableRegionMsg {
                    region_id: "tamed-tropical-lv10-01".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c2, &mut b2).await.header.type_code, TC_OK);
        let f_em = read_one_frame(&mut c2, &mut b2).await;
        assert_eq!(f_em.header.type_code, TC_EMIGRATED);
        assert_eq!(EmigratedMsg::unpack(&f_em.msgpack).unwrap().teleport_type, 0);
        drop(c2);
        srv.abort();

        // 6. Restart server against same SQLite DB -> verify persisted completed tent streams on Ready, then DestructArtifact removes it!
        let srv2 = start_server(cfg).await.unwrap();
        let saved = srv2.state.db.get_by_entity_id(&eid1).unwrap().unwrap();
        assert_eq!(saved.name, "AlphaPioneer");
        assert_eq!(saved.pos_x, 14000.0);
        assert_eq!(saved.pos_y, 16000.0);
        let saved_items = srv2.state.db.list_items(&eid1).unwrap();
        assert_eq!(saved_items.len(), 6);
        assert!(saved_items.iter().any(|it| it.item_id == crafted_knife_id && it.prototype_id == "tool_stone_knife"));
        assert!(saved_items.iter().any(|it| it.prototype_id == "clothes_student"));
        let buyer_items = srv2.state.db.list_items(&eid2).unwrap();
        assert_eq!(buyer_items.len(), 1);
        assert_eq!(buyer_items[0].prototype_id, "meat");
        let saved_eq = srv2.state.db.list_equipped(&eid1).unwrap();
        assert!(saved_eq.iter().any(|(s, id)| s == "weapon" && id == &crafted_knife_id));
        assert!(saved_eq.iter().any(|(s, _)| s == "body"));
        let saved_removed = srv2.state.removed_naturals_snapshot("tamed-grassland-lv10-01").await;
        assert!(saved_removed.contains(&(64, 71)));
        let saved_art = srv2.state.db.get_artifact(&tent_id).unwrap().unwrap();
        assert_eq!(saved_art.building_state, BUILDING_STATE_COMPLETED);

        // Reconnect AlphaPioneer -> verify AppearArtifact streams on Ready -> DestructArtifact -> DisappearEntity
        let router2 = build_http_router(srv2.state.clone());
        let r_re = router2
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=test-alpha-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s_re: Value =
            serde_json::from_slice(&r_re.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok_re = s_re["session_token"].as_str().unwrap().to_string();
        let mut c3 = TcpStream::connect(srv2.tcp_addr).await.unwrap();
        let mut b3 = Vec::new();
        c3.write_all(
            &encode_frame(
                4.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid1.clone(),
                    session_token: tok_re,
                    client_version: "5.2.1".into(),
                    device_model: "RustTest".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_WELCOME);
        c3.write_all(&encode_frame(4.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_SURVIVAL);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_STATISTICS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_SKILLS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_POINTS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_PIONEER_GRADE_INFO);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, durango_wire::TC_ESTATE_LICENSES);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_INVENTORY);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_EQUIPMENTS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_DEFOGGED_CHUNKS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_QUEST_CATEGORIES);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_CHEAT_FLAGS);
        assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_APPEAR_PLAYER);
        let mut saw_tent = false;
        let mut saw_dock = false;
        for _ in 0..2 {
            let f_re_art = read_one_frame(&mut c3, &mut b3).await;
            assert_eq!(f_re_art.header.type_code, TC_APPEAR_ARTIFACT);
            let aa = AppearArtifactMsg::unpack(&f_re_art.msgpack).unwrap();
            if aa.entity_id == tent_id {
                saw_tent = true;
            } else if aa.entity_type == 7001 {
                saw_dock = true;
            }
        }
        assert!(saw_tent && saw_dock, "expected both persisted tent and Harbor dock POI");
        for _ in 0..3 {
            assert_eq!(read_one_frame(&mut c3, &mut b3).await.header.type_code, TC_APPEAR_ANIMAL);
        }

        // Step 8: Verify all 7 Unity UI AddOnReady queries respond cleanly without hanging
        for (idx, (req_tc, resp_tc, expect_reply_seq)) in [
            (TC_GET_SKILLS, TC_SKILLS, true),
            (TC_GET_PETS_INFO, TC_PETS_INFO, true),
            (TC_GET_ATTACHABLE_ACCESSORIES, TC_ATTACHABLE_ACCESSORIES, false),
            (TC_GET_RESISTANCE_EXP_CAPS, TC_RESISTANCE_EXP_CAPS, true),
            (TC_GET_ESTATE_LICENSES, TC_ESTATE_LICENSES, true),
            (TC_GET_AVAILABLE_EMOTIONS, TC_AVAILABLE_EMOTIONS, false),
            (TC_GET_CHEAT_FLAGS, TC_CHEAT_FLAGS, false),
        ]
        .into_iter()
        .enumerate()
        {
            let qseq = 50 + idx as u32;
            c3.write_all(
                &encode_frame(4.15, qseq, 0, req_tc, &pack_empty_struct().unwrap()).unwrap(),
            )
            .await
            .unwrap();
            let f_q = read_one_frame(&mut c3, &mut b3).await;
            assert_eq!(f_q.header.type_code, resp_tc);
            assert_eq!(f_q.header.reply_of, if expect_reply_seq { qseq } else { 0 });
            if resp_tc == TC_SKILLS {
                let sk = SkillsMsg::unpack(&f_q.msgpack).unwrap();
                assert!(sk.skill_point >= 5, "expected positive skill points after leveling");
                assert_eq!(sk.categories.len(), 13);
            } else if resp_tc == TC_PETS_INFO {
                assert_eq!(PetsInfoMsg::unpack(&f_q.msgpack).unwrap().grazable_count, 0);
            }
        }

        c3.write_all(
            &encode_frame(
                4.2,
                3,
                0,
                TC_DESTRUCT_ARTIFACT,
                &DestructArtifactMsg {
                    entity_id: tent_id.clone(),
                    tile_x: 68,
                    tile_y: 72,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_destr = read_one_frame(&mut c3, &mut b3).await;
        assert_eq!(f_destr.header.type_code, TC_DESTRUCTING);
        assert!(DestructingMsg::unpack(&f_destr.msgpack).unwrap().duration > 0.0);
        let f_art_dis = read_one_frame(&mut c3, &mut b3).await;
        assert_eq!(f_art_dis.header.type_code, TC_DISAPPEAR_ENTITY);
        assert_eq!(DisappearEntity::unpack(&f_art_dis.msgpack).unwrap().entity_id, tent_id);
        assert!(srv2.state.db.get_artifact(&tent_id).unwrap().is_none());

        drop(c3);
        srv2.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn wildlife_ai_wanders_with_real_walk_clip() {
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: None,
            wildlife_wander_interval_ms: Some(120),
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        let r = router
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=dino-watcher-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r.status(), StatusCode::OK);
        let s: Value =
            serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = s["user_id"].as_str().unwrap().to_string();
        let tok = s["session_token"].as_str().unwrap().to_string();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);

        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();

        // Drain initial frames until we receive a wildlife AI TC_MOVE packet with a *_Walk clip
        let mut saw_dino_walk = false;
        for _ in 0..32 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_MOVE {
                let mv = MoveMsg::unpack(&f.msgpack).unwrap();
                if mv.entity_id.starts_with("wild-") {
                    if let Some(seg) = mv.movements.iter().find(|m| m.motion_name.ends_with("_Walk")) {
                        assert_eq!(
                            seg.motion_option,
                            durango_wire::MOTION_OPT_LOOP_ALIGN,
                            "walk animation must loop & align to path"
                        );
                        assert!(seg.path.len() >= 2, "walk path must have start and destination");
                        assert!(seg.path.last().unwrap().time > seg.path.first().unwrap().time);
                        saw_dino_walk = true;
                        break;
                    }
                }
            }
        }
        assert!(saw_dino_walk, "expected live wildlife AI TC_MOVE with *_Walk clip");

        drop(c);
        srv.abort();
    }

    #[tokio::test]
    async fn wildlife_combat_ai_counterattack_death_revive_and_respawn() {
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: None,
            wildlife_wander_interval_ms: Some(100),
            wildlife_attack_cooldown_secs: 0.15,
            wildlife_respawn_secs: 0.35,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        let r = router
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=combat-ai-tester"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value =
            serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = s["user_id"].as_str().unwrap().to_string();
        let tok = s["session_token"].as_str().unwrap().to_string();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);

        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();

        // Drain initial frames until we grab our first animal's entity_id & position
        let mut target_dino = String::new();
        let mut dino_pos = (12600.0f32, 14200.0f32);
        for _ in 0..20 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_APPEAR_ANIMAL {
                let a = AppearAnimalMsg::unpack(&f.msgpack).unwrap();
                target_dino = a.entity_id;
                dino_pos = (a.pos_x, a.pos_y);
                break;
            }
        }
        assert!(target_dino.starts_with("wild-"));

        // Put player right beside the dinosaur with low HP (20 HP) and boost dino HP so it survives 1 hit and kills the player!
        {
            let mut clients = srv.state.live_clients.write().await;
            let cl = clients.get_mut(&eid).unwrap();
            cl.pos_x = dino_pos.0 + 100.0;
            cl.pos_y = dino_pos.1;
            cl.life = 20.0;
        }
        {
            let mut animals = srv.state.animals.write().await;
            let a = animals.get_mut(&target_dino).unwrap();
            a.hp = 500.0;
            a.max_hp = 500.0;
            a.attack_damage = 25;
        }

        // Provoke the dinosaur with a barehand hit
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                TC_USE_BATTLE_ACTION,
                &UseBattleActionMsg {
                    action_id: "barehand_default_a".into(),
                    start_at: 1.2,
                    target_entity_id: Some(target_dino.clone()),
                    target_tile: None,
                    move_cancel_mode: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();

        // Wait for wildlife AI counter-attack: attack animation Move, Damaged(victim=player), EntityDied(player)
        let mut saw_dino_attack_anim = false;
        let mut saw_player_damaged = false;
        let mut saw_player_died = false;
        for _ in 0..48 {
            let f = read_one_frame(&mut c, &mut buf).await;
            match f.header.type_code {
                TC_MOVE => {
                    let mv = MoveMsg::unpack(&f.msgpack).unwrap();
                    if mv.entity_id == target_dino
                        && mv.movements.iter().any(|m| m.motion_name.contains("Attack"))
                    {
                        saw_dino_attack_anim = true;
                    }
                }
                TC_DAMAGED => {
                    let d = DamagedMsg::unpack(&f.msgpack).unwrap();
                    if d.victim_id == eid && d.attacker_id == target_dino && d.damage.value == 25 {
                        saw_player_damaged = true;
                    }
                }
                TC_ENTITY_DIED => {
                    let ed = EntityDiedMsg::unpack(&f.msgpack).unwrap();
                    if ed.entity_id == eid {
                        saw_player_died = true;
                        break;
                    }
                }
                _ => {}
            }
        }
        assert!(saw_dino_attack_anim, "expected dinosaur attack animation Move");
        assert!(saw_player_damaged, "expected dinosaur Damaged packet on player");
        assert!(saw_player_died, "expected player EntityDied");

        // Query GetReviveImmediatelyInfo -> free (cost_amount == 0)
        c.write_all(
            &encode_frame(1.5, 4, 0, TC_GET_REVIVE_IMMEDIATELY_INFO, &pack_empty_struct().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_rev_info = false;
        for _ in 0..24 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_REVIVE_IMMEDIATELY_INFO {
                let info = ReviveImmediatelyInfoMsg::unpack(&f.msgpack).unwrap();
                assert_eq!(info.cost_amount, 0);
                saw_rev_info = true;
                break;
            }
        }
        assert!(saw_rev_info);

        // Normal Revive -> Teleported(TELEPORT_TYPE_REVIVE) + Revived + EntityRevived
        c.write_all(
            &encode_frame(
                1.6,
                5,
                0,
                TC_REVIVE,
                &ReviveMsg {
                    warphole_tile: Some((63, 71)),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_tp = false;
        let mut saw_revived = false;
        let mut saw_entity_revived = false;
        for _ in 0..24 {
            let f = read_one_frame(&mut c, &mut buf).await;
            match f.header.type_code {
                TC_TELEPORTED => {
                    let tp = TeleportedMsg::unpack(&f.msgpack).unwrap();
                    assert_eq!(tp.teleport_type, TELEPORT_TYPE_REVIVE);
                    assert_eq!((tp.tile_x, tp.tile_y), (63, 71));
                    saw_tp = true;
                }
                TC_REVIVED => saw_revived = true,
                TC_ENTITY_REVIVED => {
                    let er = EntityRevivedMsg::unpack(&f.msgpack).unwrap();
                    assert_eq!(er.entity_id, eid);
                    saw_entity_revived = true;
                    break;
                }
                _ => {}
            }
        }
        assert!(saw_tp && saw_revived && saw_entity_revived);
        assert!(srv.state.live_clients.read().await[&eid].is_alive);

        // Alive player cannot abuse TC_REVIVE to teleport -> TC_ABORT
        c.write_all(
            &encode_frame(
                1.65,
                55,
                0,
                TC_REVIVE,
                &ReviveMsg { warphole_tile: None }.pack().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_alive_rev = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_alive_rev.header.type_code, durango_wire::TC_ABORT);

        // Put player back in unconscious state before testing ReviveImmediately
        {
            let mut live = srv.state.live_clients.write().await;
            let cli = live.get_mut(&eid).unwrap();
            cli.is_alive = false;
            cli.life = 0.0;
        }
        // Verify ReviveImmediately also works cleanly
        c.write_all(
            &encode_frame(
                1.7,
                6,
                0,
                TC_REVIVE_IMMEDIATELY,
                &ReviveImmediatelyMsg { voucher_id: None }.pack().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_imm_rev = false;
        for _ in 0..24 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_ENTITY_REVIVED {
                saw_imm_rev = true;
                break;
            }
        }
        assert!(saw_imm_rev);

        // Now slay the dinosaur (set its HP to 10 & move player back beside it) and verify wildlife AI respawns it automatically!
        {
            let (ax, ay) = {
                let mut animals = srv.state.animals.write().await;
                let a = animals.get_mut(&target_dino).unwrap();
                a.hp = 10.0;
                (a.pos_x, a.pos_y)
            };
            let mut clients = srv.state.live_clients.write().await;
            let cl = clients.get_mut(&eid).unwrap();
            cl.pos_x = ax + 100.0;
            cl.pos_y = ay;
        }
        c.write_all(
            &encode_frame(
                1.8,
                7,
                0,
                TC_USE_BATTLE_ACTION,
                &UseBattleActionMsg {
                    action_id: "barehand_default_a".into(),
                    start_at: 1.8,
                    target_entity_id: Some(target_dino.clone()),
                    target_tile: None,
                    move_cancel_mode: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();

        let mut saw_dino_died = false;
        let mut saw_dino_respawn = false;
        for _ in 0..64 {
            let f = read_one_frame(&mut c, &mut buf).await;
            match f.header.type_code {
                TC_ENTITY_DIED => {
                    if EntityDiedMsg::unpack(&f.msgpack).unwrap().entity_id == target_dino {
                        saw_dino_died = true;
                    }
                }
                TC_APPEAR_ANIMAL if saw_dino_died => {
                    let a = AppearAnimalMsg::unpack(&f.msgpack).unwrap();
                    if a.entity_id == target_dino && a.is_alive && a.hp == a.max_hp {
                        saw_dino_respawn = true;
                        break;
                    }
                }
                _ => {}
            }
        }
        assert!(saw_dino_died && saw_dino_respawn, "expected slain dinosaur to respawn automatically");

        drop(c);
        srv.abort();
    }

    #[tokio::test]
    async fn skills_xp_progression_levelup_and_quests_persist() {
        let db_file = std::env::temp_dir().join(format!(
            "durango_step12_test_{}.db",
            uuid::Uuid::new_v4().simple()
        ));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(db_file.clone()),
            collect_delay_ms: 10,
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg.clone()).await.unwrap();
        let router = build_http_router(srv.state.clone());

        let r = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=step12-scholar-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value =
            serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = s["session_token"].as_str().unwrap().to_string();
        let r_ch = router
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=Step12Scholar&gender=male&job=2"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r_ch.status(), StatusCode::OK);
        let ch_json: Value =
            serde_json::from_slice(&r_ch.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = ch_json["entity_id"]
            .as_str()
            .or_else(|| ch_json["id"].as_str())
            .unwrap()
            .to_string();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);
        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        // Drain initial Ready burst (OK, Survival, Statistics, Inventory, Equipments, DefoggedChunks, QuestCategories, CheatFlags, AppearPlayer, 3x AppearAnimal)
        for _ in 0..19 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // 1. GetSkills -> 13 categories at Lv 1, 10 starter Max SP (canonical community specialization curve)
        c.write_all(&encode_frame(1.2, 3, 0, TC_GET_SKILLS, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_sk = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_sk.header.type_code, TC_SKILLS);
        assert_eq!(f_sk.header.reply_of, 3);
        let sk0 = SkillsMsg::unpack(&f_sk.msgpack).unwrap();
        assert_eq!(sk0.skill_point, 10);
        assert_eq!(sk0.categories.len(), 13);
        assert_eq!(sk0.categories[&SKILL_CAT_GATHERING].level, 1);

        // 2. GetStatistics -> Lv 1, 0 XP
        c.write_all(
            &encode_frame(1.25, 4, 0, TC_GET_STATISTICS, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        let f_st = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_st.header.type_code, TC_STATISTICS);
        assert_eq!(f_st.header.reply_of, 4);
        assert_eq!(StatisticsMsg::unpack(&f_st.msgpack).unwrap(), StatisticsMsg { level: 1, exp: 0 });

        // 3. LearnSkill("gathering", lv=1, "__base__") -> OK + Skills(Max SP stays 10, never double-subtracted!) + Statistics
        c.write_all(
            &encode_frame(
                1.3,
                5,
                0,
                TC_LEARN_SKILL,
                &LearnSkillMsg {
                    skill_id: "gathering".into(),
                    level: 1,
                    sub_id: "__base__".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_l_ok = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_l_ok.header.type_code, TC_OK);
        assert_eq!(f_l_ok.header.reply_of, 5);
        let f_l_sk = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_l_sk.header.type_code, TC_SKILLS);
        let sk1 = SkillsMsg::unpack(&f_l_sk.msgpack).unwrap();
        assert_eq!(
            sk1.skill_point, sk0.skill_point,
            "SkillsMsg.skill_point is Max SP and must NEVER shrink when learning a skill"
        );
        assert!(sk1
            .skill_list
            .iter()
            .any(|b| b.skill_id == "gathering" && b.levels.get("__base__") == Some(&1)));
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_STATISTICS);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);

        // Duplicate LearnSkill at same level -> TC_FAILED; skipping level 1 -> 3 also TC_FAILED!
        c.write_all(
            &encode_frame(
                1.35,
                6,
                0,
                TC_LEARN_SKILL,
                &LearnSkillMsg {
                    skill_id: "gathering".into(),
                    level: 1,
                    sub_id: "__base__".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_dup = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_dup.header.type_code, TC_FAILED);
        assert_eq!(f_dup.header.reply_of, 6);

        // 4. ResearchSkillCategory(Gathering) at Lv 1 -> capped at char_lv (TC_FAILED); after leveling to Lv 2 -> succeeds!
        c.write_all(
            &encode_frame(
                1.38,
                70,
                0,
                TC_RESEARCH_SKILL_CATEGORY,
                &ResearchSkillCategoryMsg {
                    category: SKILL_CAT_GATHERING,
                    skip_category: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_FAILED);

        srv.state.db.update_character_level_exp(&eid, 2, 100).unwrap();
        c.write_all(
            &encode_frame(
                1.4,
                7,
                0,
                TC_RESEARCH_SKILL_CATEGORY,
                &ResearchSkillCategoryMsg {
                    category: SKILL_CAT_GATHERING,
                    skip_category: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        let f_res_sk = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_res_sk.header.type_code, TC_SKILLS);
        let sk_res = SkillsMsg::unpack(&f_res_sk.msgpack).unwrap();
        assert_eq!(sk_res.categories[&SKILL_CAT_GATHERING].level, 2);
        assert_eq!(sk_res.skill_point, 13, "Lv 2 Max SP must be 10 + (2-1)*3 = 13");
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);

        // 5. UntrainSkill("gathering", 1, "__base__") -> refunds SP + increments untrained_count
        c.write_all(
            &encode_frame(
                1.45,
                8,
                0,
                TC_UNTRAIN_SKILL,
                &UntrainSkillMsg {
                    skill_id: "gathering".into(),
                    level: 1,
                    sub_id: "__base__".into(),
                    voucher_id: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        let f_u_sk = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_u_sk.header.type_code, TC_SKILLS);
        let sk_u = SkillsMsg::unpack(&f_u_sk.msgpack).unwrap();
        assert_eq!(sk_u.skill_point, 13);
        assert_eq!(sk_u.untrained_count, 1);
        assert!(sk_u.skill_list.is_empty());
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);

        // Re-learn a skill so we can verify persistence across server restart!
        c.write_all(
            &encode_frame(
                1.5,
                9,
                0,
                TC_LEARN_SKILL,
                &LearnSkillMsg {
                    skill_id: "gathering".into(),
                    level: 1,
                    sub_id: "__base__".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_SKILLS);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_STATISTICS);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);

        // 6. Quest queries: GetQuestCategories + GetQuests
        c.write_all(
            &encode_frame(1.55, 10, 0, TC_GET_QUEST_CATEGORIES, &pack_empty_struct().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let f_qc = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_qc.header.type_code, TC_QUEST_CATEGORIES);
        assert_eq!(f_qc.header.reply_of, 10);

        c.write_all(
            &encode_frame(
                1.6,
                11,
                0,
                TC_GET_QUESTS,
                &GetQuestsMsg {
                    category: "daily".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_q = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_q.header.type_code, TC_QUESTS);
        assert_eq!(f_q.header.reply_of, 11);
        assert_eq!(QuestsMsg::unpack(&f_q.msgpack).unwrap().category, "daily");

        drop(c);
        srv.abort();

        // 7. Restart server against same SQLite DB -> verify skills, category research, and untrained_count persisted!
        let srv2 = start_server(cfg).await.unwrap();
        let persisted_skills = srv2.state.db.list_player_skills(&eid).unwrap();
        assert_eq!(persisted_skills.len(), 1);
        assert_eq!(persisted_skills[0].skill_id, "gathering");
        let persisted_cats = srv2.state.db.list_skill_categories(&eid).unwrap();
        assert_eq!(persisted_cats[&SKILL_CAT_GATHERING].0, 2);
        let persisted_ch = srv2.state.db.get_by_entity_id(&eid).unwrap().unwrap();
        assert_eq!(persisted_ch.untrained_count, 1);

        srv2.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn stacked_gathering_queue_contract_and_move_cancel() {
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: None,
            collect_delay_ms: 80,
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        let r = router
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=stack-gatherer-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value =
            serde_json::from_slice(&r.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = s["user_id"].as_str().unwrap().to_string();
        let tok = s["session_token"].as_str().unwrap().to_string();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);
        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        for _ in 0..19 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // Touch natural tile (64, 71): expect >= 2 generators (3 of first, 2 of second) all with bare_hands=1
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    entity_type: 16048,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let t = TouchedMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(t.collectible.generators.len() >= 2);
        let gen_a = t.collectible.generators[0].clone();
        let gen_b = t.collectible.generators[1].clone();
        assert_eq!(gen_a.amount, 3);
        assert_eq!(gen_b.amount, 2);
        assert_eq!(gen_a.tool_requirements.get("bare_hands"), Some(&1));
        assert_eq!(gen_b.tool_requirements.get("bare_hands"), Some(&1));

        // 1a. Start gather then walk >500 units away via Move -> must cancel cleanly with TC_CANCELED + closing ReplySequenceMark without consuming stock!
        c.write_all(
            &encode_frame(
                1.22,
                40,
                0,
                TC_COLLECT,
                &CollectMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    generator_id: gen_a.id.clone(),
                    level: 1,
                    tool_item_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_TIMER);
        c.write_all(
            &encode_frame(
                1.23,
                41,
                0,
                TC_MOVE,
                &MoveMsg {
                    entity_id: eid.clone(),
                    movements: vec![Movement {
                        motion_name: "Barehand_Run".into(),
                        motion_option: 1,
                        playback_rate: 1.0,
                        rot_speed: 6.0,
                        path: vec![Location {
                            x: 13600.0,
                            y: 14200.0,
                            yaw: 0.0,
                            time: 1.23,
                            floor: 0,
                            height: 0.0,
                        }],
                    }],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_dep_cancel = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_dep_cancel.header.type_code, durango_wire::TC_CANCELED);
        assert_eq!(f_dep_cancel.header.reply_of, 40);
        let f_dep_close = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_dep_close.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(f_dep_close.header.reply_of, 40);

        // 1b. Start gather then send explicit Cancel (2036) (sent by HudPolishMod on manual MoveStarted) -> must cancel cleanly with TC_CANCELED!
        c.write_all(
            &encode_frame(
                1.25,
                4,
                0,
                TC_COLLECT,
                &CollectMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                    generator_id: gen_a.id.clone(),
                    level: 1,
                    tool_item_id: String::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_TIMER);
        c.write_all(
            &encode_frame(1.26, 5, 0, durango_wire::TC_CANCEL, &pack_empty_struct().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let f_cancel_abort = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_cancel_abort.header.type_code, durango_wire::TC_CANCELED);
        assert_eq!(f_cancel_abort.header.reply_of, 4);
        let f_cancel_close = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_cancel_close.header.type_code, TC_REPLY_SEQUENCE_MARK);
        assert_eq!(f_cancel_close.header.reply_of, 4);

        // Verify stock is still full (3 & 2) via GetCollectible
        c.write_all(
            &encode_frame(
                1.4,
                6,
                0,
                TC_GET_COLLECTIBLE,
                &GetCollectibleMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_gc0 = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_gc0.header.type_code, TC_COLLECTIBLE);
        let gc0 = CollectibleMsg::unpack(&f_gc0.msgpack).unwrap();
        assert_eq!(gc0.generators.iter().find(|g| g.id == gen_a.id).unwrap().amount, 3);

        // 2. Realistic Attempt #1 walk-up regression test:
        //    Player starts 3 tiles away (12200, 14200), clicks bush (64,71)=(12800,14200).
        //    TC_COLLECT arrives BEFORE the 500ms-batched walk-up TC_MOVE("Barehand_Walk", 12200 -> 12720) flushes!
        //    Attempt #1 MUST SUCCEED (NEVER cancel on the buffered walk-up Move!), then stack #2 and #3!
        if let Some(cl) = srv.state.live_clients.write().await.get_mut(&eid) {
            cl.pos_x = 12200.0;
            cl.pos_y = 14200.0;
        }
        let mut p_lv = 1i32;
        let mut seq = 10u32;
        for step in 0..3 {
            c.write_all(
                &encode_frame(
                    2.0 + step as f64 * 0.2,
                    seq,
                    0,
                    TC_COLLECT,
                    &CollectMsg {
                        entity_id: String::new(),
                        tile_x: 64,
                        tile_y: 71,
                        generator_id: gen_a.id.clone(),
                        level: 1,
                        tool_item_id: String::new(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            seq += 1;
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_TIMER);
            if step == 0 {
                // Simulate BOTH a pose-transition TC_DEPART AND Unity's 500ms-batched MoveToTarget
                // walk-up packet flushing AFTER Collect #1 with a "Barehand_Walk" locomotion clip
                // ending right beside the bush (12720, 14200)! Neither may cancel Attempt #1!
                tokio::time::sleep(std::time::Duration::from_millis(25)).await;
                c.write_all(
                    &encode_frame(2.03, seq, 0, durango_wire::TC_DEPART, &pack_empty_struct().unwrap())
                        .unwrap(),
                )
                .await
                .unwrap();
                seq += 1;
                c.write_all(
                    &encode_frame(
                        2.05,
                        seq,
                        0,
                        TC_MOVE,
                        &MoveMsg {
                            entity_id: eid.clone(),
                            movements: vec![Movement {
                                motion_name: "Barehand_Walk".into(),
                                motion_option: 1,
                                playback_rate: 1.0,
                                rot_speed: 6.0,
                                path: vec![
                                    Location {
                                        x: 12200.0,
                                        y: 14200.0,
                                        yaw: 90.0,
                                        time: 1.8,
                                        floor: 0,
                                        height: 0.0,
                                    },
                                    Location {
                                        x: 12720.0,
                                        y: 14200.0,
                                        yaw: 90.0,
                                        time: 2.0,
                                        floor: 0,
                                        height: 0.0,
                                    },
                                ],
                            }],
                        }
                        .pack()
                        .unwrap(),
                    )
                    .unwrap(),
                )
                .await
                .unwrap();
                seq += 1;
            }
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_SURVIVAL);
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_INVENTORY_UPDATED);
            let fc = read_one_frame(&mut c, &mut buf).await;
            assert_eq!(fc.header.type_code, TC_COLLECTED);
            assert!(!CollectedMsg::unpack(&fc.msgpack).unwrap().ran_out);
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_REPLY_SEQUENCE_MARK);
            assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_COLLECTIBLE_CHANGED);
            let _ = read_xp_frames(&mut c, &mut buf, &mut p_lv).await;
        }

        // After 3x gen_a, GetCollectible must omit gen_a (0 left) and show gen_b = 2 with bare_hands=1
        c.write_all(
            &encode_frame(
                3.0,
                seq,
                0,
                TC_GET_COLLECTIBLE,
                &GetCollectibleMsg {
                    entity_id: String::new(),
                    tile_x: 64,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let gc1 = CollectibleMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(
            !gc1.generators.iter().any(|g| g.id == gen_a.id),
            "depleted generator should be removed from wheel while remaining generators stay active"
        );
        let gb_rem = gc1.generators.iter().find(|g| g.id == gen_b.id).unwrap();
        assert_eq!(gb_rem.amount, 2);
        assert_eq!(gb_rem.tool_requirements.get("bare_hands"), Some(&1));

        // 3. PC Movement Walk / Hold-Shift Run handshake (RunUp -> RunDown + SetBaseMoveSpeed)
        // Hello sync (kind=0, on=0) -> enabled=true, running=false, walk speed=380
        c.write_all(
            &encode_frame(3.1, seq + 1, 0, TC_RUN_UP, &RunUpMsg { kind: 0, on: 0 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let f_rd0 = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_rd0.header.type_code, TC_RUN_DOWN);
        let rd0 = RunDownMsg::unpack(&f_rd0.msgpack).unwrap();
        assert!(rd0.json.contains("\"enabled\":true") && rd0.json.contains("\"running\":false"));
        let f_sp0 = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_sp0.header.type_code, TC_SET_BASE_MOVE_SPEED);
        let sp0 = SetBaseMoveSpeedMsg::unpack(&f_sp0.msgpack).unwrap();
        assert_eq!(sp0.normal_speed, 320);

        // Hold Shift (kind=1, on=1) -> running=true, sprint speed=660
        c.write_all(
            &encode_frame(3.2, seq + 2, 0, TC_RUN_UP, &RunUpMsg { kind: 1, on: 1 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let rd1 = RunDownMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(rd1.json.contains("\"running\":true"));
        let sp1 = SetBaseMoveSpeedMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert_eq!(sp1.normal_speed, 660);

        // Release Shift (kind=1, on=0) -> running=false, walk speed=320
        c.write_all(
            &encode_frame(3.3, seq + 3, 0, TC_RUN_UP, &RunUpMsg { kind: 1, on: 0 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let rd2 = RunDownMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(rd2.json.contains("\"running\":false"));
        let sp2 = SetBaseMoveSpeedMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert_eq!(sp2.normal_speed, 320);

        drop(c);
        srv.abort();
    }

    #[tokio::test]
    async fn community_server_bugfix_and_concurrency_regression_suite() {
        let db_file = std::env::temp_dir().join(format!(
            "durango_ce_community_load_{}.db",
            uuid::Uuid::new_v4()
        ));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(db_file.clone()),
            collect_delay_ms: 10,
            craft_delay_ms: 10,
            build_delay_ms: 10,
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        // 1. Create character, verify temp_characters cleared, create solo clan -> leave -> recreate SAME clan name!
        let r_sess = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=community-test-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value =
            serde_json::from_slice(&r_sess.into_body().collect().await.unwrap().to_bytes())
                .unwrap();
        let tok = s["session_token"].as_str().unwrap().to_string();

        let r_create = router
            .clone()
            .oneshot(
                Request::post("/players")
                    .header("authorization", format!("Bearer {tok}"))
                    .header("content-type", "application/x-www-form-urlencoded")
                    .body(Body::from("name=ClanPioneer&job=1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r_create.status(), StatusCode::OK);
        let c_body: Value =
            serde_json::from_slice(&r_create.into_body().collect().await.unwrap().to_bytes())
                .unwrap();
        let eid = c_body["entity_id"].as_str().unwrap().to_string();
        assert!(
            !srv.state.temp_characters.read().await.contains_key(&eid),
            "temp_characters must be cleared once persisted so deletion cannot resurrect"
        );

        let now = ServerState::now_unix_secs();
        srv.state
            .db
            .create_clan("clan-1", "CommunityGuild", &eid, now)
            .unwrap();
        // Verify HTTP /players/:id returns real clan info!
        let r_info = router
            .clone()
            .oneshot(Request::get(format!("/players/{eid}")).body(Body::empty()).unwrap())
            .await
            .unwrap();
        let info_v: Value =
            serde_json::from_slice(&r_info.into_body().collect().await.unwrap().to_bytes())
                .unwrap();
        assert_eq!(info_v["clan"]["clan_name"].as_str(), Some("CommunityGuild"));

        // Leave solo clan -> orphan row cleaned -> name immediately reusable!
        assert!(srv.state.db.leave_clan(&eid).unwrap());
        srv.state
            .db
            .create_clan("clan-2", "CommunityGuild", &eid, now + 1.0)
            .expect("leaving solo clan must free the clan name");

        // 2. Market SQL filter past 50 rows + expired listing exclusion!
        for i in 0..60 {
            let item = crate::db::ItemRecord {
                item_id: format!("mkt-item-{i}"),
                entity_id: eid.clone(),
                prototype_id: if i == 5 { "rare_ruby".into() } else { "stone".into() },
                name: if i == 5 { "Rare Ruby".into() } else { "Stone".into() },
                icon: "icon_nat_stone".into(),
                level: 1,
                size: 1,
                durability: 100.0,
                max_durability: 100.0,
                locked: false,
                collectible_id: None,
                generator_id: None,
                created_at: now + i as f64,
            };
            srv.state
                .db
                .insert_market_product(&crate::db::MarketProductRecord {
                    product_id: format!("prod-{i}"),
                    seller_entity_id: eid.clone(),
                    region_id: "pe10gr_1".into(),
                    item,
                    price: 10,
                    listed_at: now + i as f64,
                    expires_at: if i == 0 { now - 10.0 } else { now + 3600.0 },
                })
                .unwrap();
        }
        let ruby_hits = srv
            .state
            .db
            .search_market_products(now, Some("ruby"), None, 0)
            .unwrap();
        assert_eq!(ruby_hits.len(), 1, "SQL filter must find items older than top 50");
        assert!(
            srv.state
                .db
                .take_market_product("prod-0", now)
                .unwrap()
                .is_none(),
            "expired market listing must not be purchasable"
        );

        // 3. EstimateCraft wire handler + Island Travel spawn preservation (no disconnect clobber!)
        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok.clone(),
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);
        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        for _ in 0..19 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // Send EstimateCraft -> expect CraftEstimationInfo
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                durango_wire::TC_ESTIMATE_CRAFT,
                &durango_wire::EstimateCraftMsg {
                    recipe_id: "stone_knife".into(),
                    materials: std::collections::BTreeMap::new(),
                    tool_item_id: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_est = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_est.header.type_code, durango_wire::TC_CRAFT_ESTIMATION_INFO);
        assert_eq!(f_est.header.reply_of, 3);
        let est = durango_wire::CraftEstimationInfoMsg::unpack(&f_est.msgpack).unwrap();
        assert!(est.craft_estimation.is_some());

        // Move far from origin, travel via ReturnToCamp, disconnect -> DB pos must stay (0.0, 0.0)!
        if let Some(cl) = srv.state.live_clients.write().await.get_mut(&eid) {
            cl.pos_x = 18000.0;
            cl.pos_y = 19000.0;
        }
        c.write_all(
            &encode_frame(1.3, 4, 0, durango_wire::TC_RETURN_TO_CAMP, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_EMIGRATED);
        drop(c);
        tokio::time::sleep(std::time::Duration::from_millis(60)).await;
        let ch_after_travel = srv.state.db.get_by_entity_id(&eid).unwrap().unwrap();
        assert_eq!(
            (ch_after_travel.pos_x, ch_after_travel.pos_y),
            (0.0, 0.0),
            "disconnect after emigration must NOT overwrite destination entry spawn with old coords"
        );

        // 4. Community-server concurrent DB load test: 16 simultaneous worker tasks hammering SQLite WAL
        let mut handles = Vec::new();
        for w in 0..16usize {
            let db = srv.state.db.clone();
            handles.push(tokio::spawn(async move {
                let peid = format!("stress-player-{w}");
                let rec = crate::db::CharacterRecord {
                    entity_id: peid.clone(),
                    owner_key: format!("owner-{w}"),
                    name: format!("Player{w}"),
                    job: 0,
                    level: 1,
                    region_id: "pe10gr_1".into(),
                    terrain_id: "pe10gr_1".into(),
                    pos_x: 12600.0,
                    pos_y: 14200.0,
                    disconnected_at: 0.0,
                    created_at: now,
                    gender: "male".into(),
                    model_info: String::new(),
                    exp: 0,
                    untrained_count: 0,
                    life: 100.0,
                    stamina: 100.0,
                    energy: 100.0,
                    fatigue: 0.0,
                };
                db.upsert_character(&rec).unwrap();
                for n in 0..15 {
                    let item = crate::db::ItemRecord {
                        item_id: format!("stress-item-{w}-{n}"),
                        entity_id: peid.clone(),
                        prototype_id: "wood_bough".into(),
                        name: "Branch".into(),
                        icon: "icon_nat_wood_log".into(),
                        level: 1,
                        size: 1,
                        durability: 100.0,
                        max_durability: 100.0,
                        locked: false,
                        collectible_id: None,
                        generator_id: None,
                        created_at: now,
                    };
                    db.insert_items(std::slice::from_ref(&item)).unwrap();
                    db.update_survival(&peid, 85.0, 70.0, 60.0, 15.0).unwrap();
                    db.upsert_player_skill(&peid, SKILL_CAT_GATHERING, "gathering", "__base__", 1)
                        .unwrap();
                }
                assert_eq!(db.list_items(&peid).unwrap().len(), 15);
            }));
        }
        for h in handles {
            h.await.unwrap();
        }

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn phase1_parity_recipes_estates_warp_pois_and_xp_curve() {
        let mut cfg = ServerConfig::default();
        cfg.http_bind = "127.0.0.1:0".into();
        cfg.tcp_bind = "127.0.0.1:0".into();
        cfg.collect_delay_ms = 20;
        cfg.craft_delay_ms = 20;
        cfg.build_delay_ms = 20;
        cfg.wildlife_wander_interval_ms = None;
        let db_file = std::env::temp_dir()
            .join(format!("durango_phase1_{}.db", uuid::Uuid::new_v4().simple()));
        cfg.db_path = Some(db_file.clone());
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        // 1. /season/pass returns 200 OK (no longer 404)
        let sp = router
            .clone()
            .oneshot(Request::get("/season/pass").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(sp.status(), StatusCode::OK);

        // 2. Create character and simulate a legacy Lv. 15 character with old low exp = 50
        let r_sess = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=phase1-parity-owner-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let sess: Value =
            serde_json::from_slice(&r_sess.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = sess["session_token"].as_str().unwrap().to_string();
        let r_ch = router
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=Phase1Hero&gender=male&job=1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r_ch.status(), StatusCode::OK);
        let ch_json: Value =
            serde_json::from_slice(&r_ch.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = ch_json["entity_id"]
            .as_str()
            .or_else(|| ch_json["id"].as_str())
            .unwrap()
            .to_string();
        srv.state.db.update_character_level_exp(&eid, 15, 50).unwrap();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok.clone(),
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let w = Welcome::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert_eq!(w.region.id, "tamed-grassland-lv10-01");
        assert_eq!(w.personal_region_id.as_deref(), Some("tamed-grassland-lv10-01"));
        assert_eq!(w.region.role, durango_wire::RegionRole::Personal);

        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_SURVIVAL);
        // Official cumulative XP migration: Lv. 15 with exp=50 auto-bumps to 1784 (0.0%, never -66.5%!)
        let st = StatisticsMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert_eq!(st.level, 15);
        assert_eq!(st.exp, 1784);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_SKILLS);
        let init_rec = durango_wire::RecipesMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(!init_rec.ids.is_empty(), "starter recipes must not be empty");
        let init_bp = durango_wire::ArtifactBlueprintsMsg::unpack(&read_one_frame(&mut c, &mut buf).await.msgpack).unwrap();
        assert!(!init_bp.ids.is_empty(), "starter blueprints must not be empty");
        // Drain rest of Ready burst (Points, PioneerGrade, EstateLicenses, Inventory, Equipments, Defog, QuestCats, CheatFlags, AppearPlayer, Dock POI, 3x Animal)
        for _ in 0..13 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // 3. Explicit GetRecipes & GetArtifactBlueprints queries return non-empty catalogs
        c.write_all(&encode_frame(1.2, 10, 0, durango_wire::TC_GET_RECIPES, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_rec = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_rec.header.type_code, durango_wire::TC_RECIPES);
        assert_eq!(f_rec.header.reply_of, 10);
        assert!(!durango_wire::RecipesMsg::unpack(&f_rec.msgpack).unwrap().ids.is_empty());

        c.write_all(&encode_frame(1.25, 11, 0, durango_wire::TC_GET_ARTIFACT_BLUEPRINTS, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_bp = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_bp.header.type_code, durango_wire::TC_ARTIFACT_BLUEPRINTS);
        assert_eq!(f_bp.header.reply_of, 11);
        assert!(!durango_wire::ArtifactBlueprintsMsg::unpack(&f_bp.msgpack).unwrap().ids.is_empty());

        // 4. Domain -> Tamed Island query (GetPersonalRegionInfo) resolves immediately without spinner
        c.write_all(&encode_frame(1.3, 12, 0, durango_wire::TC_GET_PERSONAL_REGION_INFO, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_pr = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_pr.header.type_code, durango_wire::TC_PERSONAL_REGION_INFO);
        assert_eq!(f_pr.header.reply_of, 12);

        // 5. Declare Estate -> persisted in SQLite + broadcasts EstateGrids + EstateLicenses
        let dec_buf = durango_wire::DeclareEstateMsg {
            owner_type: durango_wire::OWNER_TYPE_PERSONAL_PLAYER,
            cell_x: 16,
            cell_y: 17,
        }
        .pack()
        .unwrap();
        c.write_all(&encode_frame(1.35, 13, 0, durango_wire::TC_DECLARE_ESTATE, &dec_buf).unwrap())
            .await
            .unwrap();
        let f_est = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_est.header.type_code, durango_wire::TC_ESTATE_LICENSE);
        assert_eq!(f_est.header.reply_of, 13);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ESTATE_GRIDS);
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ESTATE_LICENSES);
        assert_eq!(srv.state.db.list_estates_by_owner(&eid).unwrap().len(), 1);

        // 6. WarpToPort (9081241) -> FIRST & ONLY reply at seq=14 is Timer(1134), then Teleported(2037) at reply_of=0 with TeleportType::Returning (1)
        c.write_all(&encode_frame(1.4, 14, 0, durango_wire::TC_WARP_TO_PORT, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let f_t = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_t.header.type_code, TC_TIMER);
        assert_eq!(f_t.header.reply_of, 14);
        let f_tp = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_tp.header.type_code, TC_TELEPORTED);
        assert_eq!(f_tp.header.reply_of, 0);
        let tp = TeleportedMsg::unpack(&f_tp.msgpack).unwrap();
        assert_eq!((tp.tile_x, tp.tile_y), (63, 71));
        assert_eq!(tp.teleport_type, durango_wire::TELEPORT_TYPE_RETURNING);

        // 7. GetPOICount & GetExploredPOIs include the Harbor port at (63, 71)
        let rid_buf = durango_wire::SingleStringMsg {
            value: "pe10gr_1".into(),
        }
        .pack()
        .unwrap();
        c.write_all(&encode_frame(1.45, 15, 0, durango_wire::TC_GET_POI_COUNT, &rid_buf).unwrap())
            .await
            .unwrap();
        let f_pc = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_pc.header.type_code, durango_wire::TC_POI_COUNT);
        assert_eq!(f_pc.header.reply_of, 15);

        c.write_all(&encode_frame(1.5, 16, 0, durango_wire::TC_GET_EXPLORED_POIS, &rid_buf).unwrap())
            .await
            .unwrap();
        let f_ep = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_ep.header.type_code, durango_wire::TC_EXPLORED_POIS);
        assert_eq!(f_ep.header.reply_of, 16);

        // 8. GetPioneerGradeInfo & GetDiscoveryInfo & GetRoutes all respond cleanly
        c.write_all(&encode_frame(1.55, 17, 0, durango_wire::TC_GET_PIONEER_GRADE_INFO, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_PIONEER_GRADE_INFO);

        c.write_all(&encode_frame(1.6, 18, 0, durango_wire::TC_GET_DISCOVERY_INFO, &rid_buf).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_DISCOVERY_INFO);

        c.write_all(&encode_frame(1.65, 19, 0, durango_wire::TC_GET_ROUTES, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, durango_wire::TC_ROUTES);

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn phase2a_wildlife_aggro_herds_repair_dump_and_status() {
        // 1. Verify TerrainHerds YAML parser reads beach + land herd points cleanly
        let sample_herds = "\
herds:
  beach:
  - id: 201
    tile:
    - 188
    - 192
  land:
  - id: 301
    tile: [90, 110]
chunks:
  foo: bar
";
        let parsed_herds = crate::terrain::TerrainHerds::parse_yaml(sample_herds);
        assert_eq!(parsed_herds.beach, vec![(188, 192)]);
        assert_eq!(parsed_herds.land, vec![(90, 110)]);

        // 2. Start server with fast wildlife AI loop to verify Carnivore proximity aggro + Phase 2A handlers
        let mut cfg = ServerConfig::default();
        cfg.http_bind = "127.0.0.1:0".into();
        cfg.tcp_bind = "127.0.0.1:0".into();
        cfg.collect_delay_ms = 20;
        cfg.wildlife_wander_interval_ms = Some(100);
        cfg.wildlife_attack_cooldown_secs = 0.25;
        let db_file = std::env::temp_dir().join(format!(
            "durango_phase2a_{}.db",
            uuid::Uuid::new_v4().simple()
        ));
        cfg.db_path = Some(db_file.clone());
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        let r_sess = router
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=phase2a-hunter-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let sess: Value =
            serde_json::from_slice(&r_sess.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = sess["session_token"].as_str().unwrap().to_string();
        let r_ch = router
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=Phase2Hunter&gender=male&job=0"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let ch_json: Value =
            serde_json::from_slice(&r_ch.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = ch_json["entity_id"]
            .as_str()
            .or_else(|| ch_json["id"].as_str())
            .unwrap()
            .to_string();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);
        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        for _ in 0..19 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // 3. Make one dinosaur an aggressive predator placed right in front of the player -> must proximity-aggro!
        {
            let (px, py) = {
                let live = srv.state.live_clients.read().await;
                let cl = &live[&eid];
                (cl.pos_x, cl.pos_y)
            };
            let mut animals = srv.state.animals.write().await;
            let a = animals.values_mut().next().unwrap();
            a.is_aggressive = true;
            a.pos_x = px + 300.0;
            a.pos_y = py;
            a.home_x = px + 300.0;
            a.home_y = py;
            a.walk_from_x = a.pos_x;
            a.walk_from_y = a.pos_y;
            a.walk_start_at = 0.0;
            a.walk_end_at = 0.0;
            a.stop_walking_at = 0.0;
        }

        let mut saw_aggro_ci = false;
        let mut saw_battle_begun = false;
        for _ in 0..32 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_COMBAT_INTERACTION {
                let ci = durango_wire::CombatInteractionMsg::unpack(&f.msgpack).unwrap();
                if ci.details.get("status") == Some(&durango_wire::ANIMAL_STATUS_BATTLE) {
                    saw_aggro_ci = true;
                }
            } else if f.header.type_code == TC_BATTLE_BEGUN {
                saw_battle_begun = true;
            }
            if saw_aggro_ci && saw_battle_begun {
                break;
            }
        }
        assert!(
            saw_aggro_ci && saw_battle_begun,
            "expected aggressive dinosaur proximity aggro (CombatInteraction + BattleBegun)"
        );

        // 4. Test RepairItem + DumpItems + DrinkWater + GetStatusEffects
        let now = crate::state::ServerState::now_unix_secs();
        let worn_item = crate::db::ItemRecord {
            item_id: "worn-knife-1".into(),
            entity_id: eid.clone(),
            prototype_id: "tool_stone_knife".into(),
            name: "Stone Knife".into(),
            icon: "icon_tool_knife".into(),
            level: 1,
            size: 1,
            durability: 40.0,
            max_durability: 100.0,
            locked: false,
            collectible_id: None,
            generator_id: None,
            created_at: now,
        };
        let repair_kit = crate::db::ItemRecord {
            item_id: "repair-kit-1".into(),
            entity_id: eid.clone(),
            prototype_id: "tool_repair_kit".into(),
            name: "Repair Kit".into(),
            icon: "icon_tool_repair".into(),
            level: 1,
            size: 1,
            durability: 100.0,
            max_durability: 100.0,
            locked: false,
            collectible_id: None,
            generator_id: None,
            created_at: now,
        };
        srv.state
            .db
            .insert_items(&[worn_item, repair_kit])
            .unwrap();

        // Empty kit list must be rejected with TC_ABORT
        c.write_all(
            &encode_frame(
                1.95,
                29,
                0,
                durango_wire::TC_REPAIR_ITEM,
                &durango_wire::RepairItemMsg {
                    item_id: "worn-knife-1".into(),
                    kit_item_ids: Vec::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_ABORT && f.header.reply_of == 29 {
                break;
            }
        }

        c.write_all(
            &encode_frame(
                2.0,
                30,
                0,
                durango_wire::TC_REPAIR_ITEM,
                &durango_wire::RepairItemMsg {
                    item_id: "worn-knife-1".into(),
                    kit_item_ids: vec!["repair-kit-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_OK && f.header.reply_of == 30 {
                break;
            }
        }
        let repaired = srv
            .state
            .db
            .list_items(&eid)
            .unwrap()
            .into_iter()
            .find(|i| i.item_id == "worn-knife-1")
            .unwrap();
        assert_eq!(repaired.durability, 100.0);

        c.write_all(
            &encode_frame(
                2.1,
                31,
                0,
                durango_wire::TC_DUMP_ITEMS,
                &durango_wire::DumpItemsMsg {
                    item_ids: vec!["worn-knife-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == TC_OK && f.header.reply_of == 31 {
                break;
            }
        }
        assert!(srv
            .state
            .db
            .list_items(&eid)
            .unwrap()
            .iter()
            .all(|i| i.item_id != "worn-knife-1"));

        c.write_all(
            &encode_frame(
                2.2,
                32,
                0,
                durango_wire::TC_GET_STATUS_EFFECTS,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_STATUS_EFFECTS && f.header.reply_of == 32 {
                break;
            }
        }

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn phase2b_wallet_market_payouts_containers_warehouses_and_mail() {
        let mut cfg = ServerConfig::default();
        cfg.http_bind = "127.0.0.1:0".into();
        cfg.tcp_bind = "127.0.0.1:0".into();
        cfg.collect_delay_ms = 20;
        cfg.wildlife_wander_interval_ms = None;
        let db_file = std::env::temp_dir().join(format!(
            "durango_phase2b_{}.db",
            uuid::Uuid::new_v4().simple()
        ));
        cfg.db_path = Some(db_file.clone());
        let srv = start_server(cfg).await.unwrap();
        let router = build_http_router(srv.state.clone());

        // Create Seller (A) and Buyer (B)
        let mk_user = |acct: &'static str, name: &'static str| {
            let r = router.clone();
            async move {
                let rs = r
                    .clone()
                    .oneshot(Request::post("/sessions").body(Body::from(format!("account_id={acct}"))).unwrap())
                    .await
                    .unwrap();
                let s: Value = serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
                let tok = s["session_token"].as_str().unwrap().to_string();
                let rc = r
                    .oneshot(
                        Request::post("/players")
                            .header(header::AUTHORIZATION, &tok)
                            .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                            .body(Body::from(format!("name={name}&gender=male&job=0")))
                            .unwrap(),
                    )
                    .await
                    .unwrap();
                let c: Value = serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
                let eid = c["entity_id"].as_str().or_else(|| c["id"].as_str()).unwrap().to_string();
                (eid, tok)
            }
        };
        let (eid_a, tok_a) = mk_user("p2b-seller-01", "SellerA").await;
        let (eid_b, tok_b) = mk_user("p2b-buyer-01", "BuyerB").await;

        let now = crate::state::ServerState::now_unix_secs();
        let seed_item = |id: &str, owner: &str| crate::db::ItemRecord {
            item_id: id.into(),
            entity_id: owner.into(),
            prototype_id: "wood_log".into(),
            name: "Log".into(),
            icon: "icon_nat_wood_log".into(),
            level: 5,
            size: 1,
            durability: 100.0,
            max_durability: 100.0,
            locked: false,
            collectible_id: None,
            generator_id: None,
            created_at: now,
        };
        srv.state
            .db
            .insert_items(&[
                seed_item("item-mkt-1", &eid_a),
                seed_item("item-chest-1", &eid_a),
                seed_item("item-mail-1", &eid_a),
            ])
            .unwrap();

        let connect = |eid: String, tok: String, addr: std::net::SocketAddr| async move {
            let mut c = TcpStream::connect(addr).await.unwrap();
            let mut b = Vec::new();
            c.write_all(
                &encode_frame(
                    1.0,
                    1,
                    0,
                    TC_AUTH,
                    &Auth {
                        entity_id: eid,
                        session_token: tok,
                        client_version: "3.3.6".into(),
                        device_model: "Linux".into(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            assert_eq!(read_one_frame(&mut c, &mut b).await.header.type_code, TC_WELCOME);
            c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
                .await
                .unwrap();
            let mut inv_tstone = 0i64;
            for _ in 0..19 {
                let f = read_one_frame(&mut c, &mut b).await;
                if f.header.type_code == TC_INVENTORY {
                    inv_tstone = InventoryMsg::unpack(&f.msgpack).unwrap().t_stone;
                }
            }
            (c, b, inv_tstone)
        };

        let (mut ca, mut ba, ts_a) = connect(eid_a.clone(), tok_a, srv.tcp_addr).await;
        let (mut cb, mut bb, ts_b) = connect(eid_b.clone(), tok_b, srv.tcp_addr).await;
        assert_eq!(ts_a, 50_000, "new characters start with 50,000 T-Stone");
        assert_eq!(ts_b, 50_000);
        // Drain peer AppearPlayer frames
        let _ = read_one_frame(&mut ca, &mut ba).await;
        let _ = read_one_frame(&mut cb, &mut bb).await;

        // 1. Seller A lists item-mkt-1 for 1,200 T-Stone -> Buyer B buys -> Seller A collects 1,200 T-Stone!
        ca.write_all(
            &encode_frame(
                2.0,
                10,
                0,
                TC_REGISTER_PRODUCT,
                &RegisterProductMsg {
                    item_id: "item-mkt-1".into(),
                    price: 1200,
                    duration: 24.0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_OK);

        // Seller queries GetRegisteredProducts
        ca.write_all(
            &encode_frame(2.05, 11, 0, durango_wire::TC_GET_REGISTERED_PRODUCTS, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        let f_reg = read_one_frame(&mut ca, &mut ba).await;
        assert_eq!(f_reg.header.type_code, TC_PRODUCTS);
        let reg = ProductsMsg::unpack(&f_reg.msgpack).unwrap();
        assert_eq!(reg.products.len(), 1);
        let pid = reg.products[0].id.clone();

        // Buyer B buys product -> loses 1,200 T-Stone (48,800) & gets WalletUpdated
        cb.write_all(&encode_frame(2.1, 20, 0, TC_BUY_PRODUCT, &BuyProductMsg { product_id: pid.clone(), payment_ids: vec![] }.pack().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, TC_OK);
        let f_wb = read_one_frame(&mut cb, &mut bb).await;
        assert_eq!(f_wb.header.type_code, durango_wire::TC_WALLET_UPDATED);
        assert_eq!(durango_wire::WalletUpdatedMsg::unpack(&f_wb.msgpack).unwrap().t_stone, 48_800);

        // Seller A collects payment -> gains 1,200 T-Stone (51,200)
        ca.write_all(
            &encode_frame(2.2, 12, 0, durango_wire::TC_MARKET_COLLECT_ALL_PAYMENTS, &pack_empty_struct().unwrap()).unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_OK);
        let f_wa = read_one_frame(&mut ca, &mut ba).await;
        assert_eq!(f_wa.header.type_code, durango_wire::TC_WALLET_UPDATED);
        assert_eq!(durango_wire::WalletUpdatedMsg::unpack(&f_wa.msgpack).unwrap().t_stone, 51_200);

        // 2. Storage Container PutInItem -> TakeOutItem roundtrip
        let ch_a = srv.state.db.get_by_entity_id(&eid_a).unwrap().unwrap();
        srv.state
            .db
            .upsert_artifact(&crate::db::ArtifactRecord {
                entity_id: "chest-1".into(),
                region_id: ch_a.region_id.clone(),
                entity_type: 3102,
                blueprint_id: "bamboo_basket".into(),
                tile_x: 65,
                tile_y: 71,
                size_x: 1,
                size_y: 1,
                height: 1,
                rotation: 0,
                building_state: durango_wire::BUILDING_STATE_COMPLETED,
                founder_entity_id: eid_a.clone(),
                level: 1,
                created_at: now,
                durability: 100.0,
                max_durability: 100.0,
                last_decay_at: now,
                gate_opened: false,
                is_lit: false,
                materials_json: "{}".into(),
            })
            .unwrap();
        let put_req = durango_wire::ContainerItemsOpMsg {
            entity_id: "chest-1".into(),
            tile_x: 65,
            tile_y: 71,
            item_ids: vec!["item-chest-1".into()],
        };
        ca.write_all(&encode_frame(2.3, 13, 0, durango_wire::TC_PUT_IN_ITEM, &put_req.pack().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_OK);
        assert_eq!(srv.state.db.list_container_items("chest-1", None).unwrap().len(), 1);

        ca.write_all(&encode_frame(2.35, 14, 0, durango_wire::TC_TAKE_OUT_ITEM, &put_req.pack().unwrap()).unwrap())
            .await
            .unwrap();
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_OK);
        assert_eq!(srv.state.db.list_container_items("chest-1", None).unwrap().len(), 0);

        // 3. In-Game Mail: Seller A mails item-mail-1 to Buyer B -> Buyer B receives live TC_MAILS & accepts attachment!
        ca.write_all(
            &encode_frame(
                2.4,
                15,
                0,
                durango_wire::TC_SEND_MAIL,
                &durango_wire::SendMailMsg {
                    recipient_id: eid_b.clone(),
                    text: "Gift log for trading!".into(),
                    item_ids: vec!["item-mail-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut ca, &mut ba).await.header.type_code, TC_OK);

        let f_mail_b = read_one_frame(&mut cb, &mut bb).await;
        assert_eq!(f_mail_b.header.type_code, durango_wire::TC_MAILS);
        let mails_b = srv.state.db.list_mails(&eid_b).unwrap();
        assert_eq!(mails_b.len(), 1);
        let mail_id = mails_b[0].mail_id.clone();

        cb.write_all(
            &encode_frame(
                2.45,
                21,
                0,
                durango_wire::TC_ACCEPT_USER_MAILS,
                &durango_wire::MailIdsMsg {
                    mail_ids: vec![mail_id],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, TC_INVENTORY_UPDATED);
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, TC_OK);
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, durango_wire::TC_MAILS);
        assert!(srv.state.db.list_items(&eid_b).unwrap().iter().any(|i| i.item_id == "item-mail-1"));

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn community_directory_join_password_and_admin_blacklist() {
        let db_file = std::env::temp_dir()
            .join(format!("durango_comm_{}.db", uuid::Uuid::new_v4().simple()));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            public_host: "127.0.0.1".into(),
            server_name: "Alpha Community Hub".into(),
            db_path: Some(db_file.clone()),
            wildlife_wander_interval_ms: None,
            join_password: Some("secret-pass-42".into()),
            admin_token: Some("adm-tok-999".into()),
            admin_keys: vec!["admin-owner-1".into()],
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.expect("start server");
        let app = build_http_router(srv.state.clone());

        // 1. Live Directory: remote server registers via heartbeat -> /servers.json returns self [Locked] + remote
        let hb_body = serde_json::json!({
            "server_id": "eu-pve-1",
            "name": "EU Cozy PvE",
            "gateway": "http://198.51.100.10:8190",
            "region": "EU",
            "online_players": 14,
            "max_players": 60,
            "password_required": false
        });
        let resp = app
            .clone()
            .oneshot(
                Request::post("/api/directory/heartbeat")
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(hb_body.to_string()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);

        let resp = app
            .clone()
            .oneshot(Request::get("/servers.json").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        let sj: Value = serde_json::from_slice(&resp.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let servers = sj["servers"].as_array().unwrap();
        assert_eq!(servers.len(), 2);
        assert_eq!(servers[0]["name"], "Alpha Community Hub [Locked]");
        assert_eq!(servers[0]["password_required"], true);
        assert_eq!(servers[1]["name"], "EU Cozy PvE");
        assert_eq!(servers[1]["gateway"], "http://198.51.100.10:8190");

        // /directory HTML page renders cleanly
        let resp = app
            .clone()
            .oneshot(Request::get("/directory").body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        let dir_html = String::from_utf8(resp.into_body().collect().await.unwrap().to_bytes().to_vec()).unwrap();
        assert!(dir_html.contains("Alpha Community Hub"));
        assert!(dir_html.contains("EU Cozy PvE"));

        // 2. Join Password Enforcement:
        // 2a. Missing password -> 403 web_login_required
        let resp = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("account_id=player-one-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::FORBIDDEN);
        let err_json: Value = serde_json::from_slice(&resp.into_body().collect().await.unwrap().to_bytes()).unwrap();
        assert_eq!(err_json["error"]["code"], "web_login_required");

        // 2b. Correct password via X-Durango-Join-Password header -> 200 OK
        let resp = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .header("x-durango-join-password", "secret-pass-42")
                    .body(Body::from("account_id=player-one-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        let s1: Value = serde_json::from_slice(&resp.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok1 = s1["session_token"].as_str().unwrap().to_string();

        // 2c. Correct password via /p/<password>/sessions URL prefix (stock Android APK path!) -> 200 OK
        let resp = app
            .clone()
            .oneshot(
                Request::post("/p/secret-pass-42/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("account_id=player-two-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);

        // Create character for player-one and connect via TCP
        let resp = app
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, format!("Bearer {tok1}"))
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=Troublemaker&job=0&region=pe10gr_1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        let p1: Value = serde_json::from_slice(&resp.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid1 = p1["entity_id"].as_str().unwrap().to_string();

        let mut c1 = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut b1 = Vec::new();
        c1.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid1.clone(),
                    session_token: tok1.clone(),
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c1, &mut b1).await.header.type_code, TC_WELCOME);
        c1.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        // Drain initial Ready frames
        for _ in 0..19 {
            let _ = read_one_frame(&mut c1, &mut b1).await;
        }

        // 3. Admin API auth check (wrong token -> 401)
        let resp = app
            .clone()
            .oneshot(
                Request::get("/admin/bans")
                    .header("x-admin-token", "wrong-tok")
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::UNAUTHORIZED);

        // 4. Admin bans Troublemaker by character name -> auto-bans account + entity AND immediately kicks live TCP socket!
        let ban_req = serde_json::json!({
            "ban_type": "character",
            "target": "Troublemaker",
            "reason": "Griefing estates",
            "duration_hours": 24
        });
        let resp = app
            .clone()
            .oneshot(
                Request::post("/admin/bans")
                    .header("x-admin-token", "adm-tok-999")
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(ban_req.to_string()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        let ban_resp: Value = serde_json::from_slice(&resp.into_body().collect().await.unwrap().to_bytes()).unwrap();
        assert_eq!(ban_resp["kicked"], 1);
        let created_ids = ban_resp["created_ids"].as_array().unwrap();
        assert!(created_ids.len() >= 2);

        // Live TCP connection receives TC_ABORT kick frame immediately
        let kick_frame = tokio::time::timeout(
            std::time::Duration::from_secs(2),
            read_one_frame(&mut c1, &mut b1),
        )
        .await
        .expect("kicked player should receive abort");
        assert_eq!(kick_frame.header.type_code, durango_wire::TC_ABORT);

        // Banned account cannot re-login via HTTP even with correct join password
        let resp = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .header("x-durango-join-password", "secret-pass-42")
                    .body(Body::from("account_id=player-one-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::FORBIDDEN);

        // 5. CIDR IP ban blocks alt accounts on matching subnet
        let ip_ban_req = serde_json::json!({
            "ban_type": "ip",
            "target": "203.0.113.0/24",
            "reason": "Alt evasion"
        });
        let resp = app
            .clone()
            .oneshot(
                Request::post("/admin/bans")
                    .header("x-admin-token", "adm-tok-999")
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(ip_ban_req.to_string()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);

        let resp = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .header("x-durango-join-password", "secret-pass-42")
                    .header("x-forwarded-for", "203.0.113.77")
                    .body(Body::from("account_id=brand-new-alt"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::FORBIDDEN);

        // 6. Admin live config update: remove join password -> /status reports password_required=false
        let resp = app
            .clone()
            .oneshot(
                Request::post("/admin/config")
                    .header("x-admin-token", "adm-tok-999")
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(serde_json::json!({ "join_password": "" }).to_string()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);
        assert!(srv.state.effective_join_password().is_none());

        // Unban Troublemaker's rules -> can log in again without password
        for id_v in created_ids {
            let id = id_v.as_i64().unwrap();
            let resp = app
                .clone()
                .oneshot(
                    Request::delete(format!("/admin/bans/{id}"))
                        .header("x-admin-token", "adm-tok-999")
                        .body(Body::empty())
                        .unwrap(),
                )
                .await
                .unwrap();
            assert_eq!(resp.status(), StatusCode::OK);
        }
        let resp = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("account_id=player-one-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK);

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn playtest_fixes_moss_hut_stone_blade_ecology_pioneer_fatigue_and_domain_durability() {
        let db_file = std::env::temp_dir()
            .join(format!("durango_playtest_{}.db", uuid::Uuid::new_v4().simple()));
        let assets_path = std::path::PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../../_backup_2026-11-19/workspace-artifacts/build/server/data/assets");
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            public_host: "127.0.0.1".into(),
            assets_dir: if assets_path.is_dir() { Some(assets_path) } else { None },
            db_path: Some(db_file.clone()),
            wildlife_wander_interval_ms: None,
            build_delay_ms: 10,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.expect("start server");

        // 1. Stone Blade carries real weapon Performance (slot="main") so Character -> Gear can equip it!
        let blade_perfs = srv.state.catalog.item_performances("blade_stone", 1);
        assert!(
            blade_perfs.iter().any(|p| p.id == "weapon"
                && p.strs.get("slot").map(String::as_str) == Some("main")),
            "blade_stone must carry weapon Performance with slot=main, got {blade_perfs:?}"
        );

        // 2. Moss Hut (s02_shelter_01) resolves non-empty 3D model Parts!
        let moss_spec = srv.state.catalog.resolve_blueprint("s02_shelter_01");
        let moss_parts = srv
            .state
            .catalog
            .artifact_default_parts(moss_spec.entity_type, "s02_shelter_01");
        assert!(
            !moss_parts.is_empty(),
            "Moss Hut must resolve non-empty ArtifactDisplay.Parts for 3D rendering"
        );

        // 3. Tamed Island wildlife ecology excludes Brachiosaurus, Dodophysis, and aggressive predators!
        let (safe_peaceful, safe_aggro) = srv.state.catalog.island_wildlife_pool(10, true);
        assert!(safe_aggro.is_empty(), "Tamed Island must not spawn aggressive predators");
        for a in &safe_peaceful {
            let low = a.raw_id.to_ascii_lowercase();
            assert!(!low.contains("brachio"), "Brachiosaurus must NOT spawn on Tamed Island");
            assert!(!low.contains("dodo"), "Dodophysis must NOT spawn on Tamed Island");
            assert!(a.size_level <= 2 && a.difficulty <= 1.2);
        }

        // 4. Gradual Rest Fatigue: resting reduces fatigue by 12 instead of wiping it to 0 immediately!
        let (eid, tok) = srv.state.issue_session("playtest-owner-1").await;
        let ch = crate::db::CharacterRecord {
            entity_id: eid.clone(),
            owner_key: "playtest-owner-1".into(),
            name: "Playtester".into(),
            job: 0,
            level: 3,
            region_id: "pe10gr_1".into(),
            terrain_id: "pe10gr_1".into(),
            pos_x: 63.0,
            pos_y: 71.0,
            disconnected_at: 0.0,
            created_at: 1.0,
            gender: "male".into(),
            model_info: String::new(),
            exp: 30,
            untrained_count: 0,
            life: 60.0,
            stamina: 50.0,
            energy: 50.0,
            fatigue: 60.0,
        };
        srv.state.db.upsert_character(&ch).unwrap();
        srv.state.db.update_survival(&eid, 60.0, 50.0, 50.0, 60.0).unwrap();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok.clone(),
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);

        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        let mut saw_pioneer_grade_1 = false;
        let expected_pg = durango_wire::PioneerGradeInfoMsg {
            entity_id: eid.clone(),
            grade: 1,
            point: 0.0,
            point_needed: 100,
            current_maximum_estate_size: 15,
            current_access_level: 1,
        }
        .pack()
        .unwrap();
        for _ in 0..19 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_PIONEER_GRADE_INFO {
                assert_eq!(f.msgpack, expected_pg, "Lv 3 character must start at Pioneer Grade 1");
                saw_pioneer_grade_1 = true;
            }
        }
        assert!(saw_pioneer_grade_1);

        // Rest starts CONTINUOUS gradual fatigue decrease (downward GaugeMsg ramp + tick_rest decreases continuously)
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                TC_REST_ON,
                &RestOnMsg {
                    entity_id: "any-hut".into(),
                    tile_x: 63,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_OK);
        let f_surv = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_surv.header.type_code, TC_SURVIVAL);
        let surv = SurvivalMsg::unpack(&f_surv.msgpack).unwrap();
        let fat_nodes = &surv.gauges.get("fatigue").unwrap().nodes;
        assert_eq!(fat_nodes.len(), 2, "Resting fatigue gauge must be a continuous downward ramp");
        assert!((fat_nodes[0].1 - 60.0).abs() < 0.1);
        assert!((fat_nodes[1].1 - 0.0).abs() < 0.01);
        let f_fv = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_fv.header.type_code, durango_wire::TC_FATIGUE_VELOCITIES);
        assert!(
            (durango_wire::FatigueVelocitiesMsg::unpack(&f_fv.msgpack)
                .unwrap()
                .velocities[&0]
                + 2.0)
                .abs()
                < 0.01
        );
        // Simulate 5 seconds of staying resting -> fatigue drops continuously from 60 -> 50
        {
            let mut live = srv.state.live_clients.write().await;
            let cli = live.get_mut(&eid).unwrap();
            assert!(cli.is_resting);
            let t0 = cli.rest_updated_at;
            assert!(cli.tick_rest(t0 + 5.0));
            assert!((cli.fatigue - 50.0).abs() < 0.1, "5s rest should drop fatigue 60 -> 50, got {}", cli.fatigue);
            assert!(cli.stop_rest(t0 + 5.0));
            assert!(!cli.is_resting);
        }
        // Verify canonical SWE Account ID (`usr_<24-hex>` Option A SHA-256) and Zone Code (`tamed-grassland-lv10-01`)
        // are stored directly as authoritative data (not just display labels!)
        let digest_abc = crate::state::sha256(b"abc");
        let hex_abc: String = digest_abc.iter().map(|b| format!("{b:02x}")).collect();
        assert_eq!(
            hex_abc,
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        );
        let acct_a = crate::state::canonical_account_id_from_secret("ef72cdc18325c48857551c3c").unwrap();
        assert_eq!(acct_a.len(), 28);
        assert!(acct_a.starts_with("usr_"));
        assert_eq!(
            crate::state::format_account_tag("ef72cdc18325c48857551c3c"),
            acct_a
        );
        // Replaying the canonical usr_<24-hex> string as a login secret must NOT yield the same ID!
        assert_ne!(
            crate::state::canonical_account_id_from_secret(&acct_a).unwrap(),
            acct_a
        );
        assert_eq!(crate::state::public_account_tag(&acct_a), &acct_a[..16]);
        assert_eq!(
            crate::state::format_region_code("pe10gr_1"),
            "tamed-grassland-lv10-01"
        );
        assert_eq!(
            crate::state::terrain_id_for_region("tamed-grassland-lv10-01"),
            "pe10gr_1"
        );
        assert_eq!(
            crate::state::format_region_code("ru25fo_2"),
            "unstable-forest-lv25-02"
        );
        assert_eq!(
            crate::state::region_display_name("tamed-grassland-lv10-01"),
            "Tamed Island · Lv.10 Grassland"
        );
        // WaterData::ByteToDepth check:
        //   0..=127  -> ocean depth = b/127.0  (dry when depth < 0.30, i.e. b <= 38)
        //   128..=255 -> lake depth = (b-128)/127.0 (dry when depth < 0.10, i.e. 128..=140)
        let mut t = crate::terrain::LoadedTerrain::fallback("pe10gr_1");
        let stride = t.width + 1;
        t.ocean[10 + 10 * stride] = 127; // deep ocean
        assert!(!t.is_dry_land(10, 10));
        assert!(!t.is_safe_inland_land(10, 10));
        for &(cx, cy) in &[(15usize, 15usize), (16, 15), (15, 16), (16, 16)] {
            t.ocean[cx + cy * stride] = 15; // dry coastal beach (depth ~0.118 < 0.30)
        }
        assert!(t.is_dry_land(15, 15));
        for &(cx, cy) in &[(20usize, 20usize), (21, 20), (20, 21), (21, 21)] {
            t.ocean[cx + cy * stride] = 183; // deep inland lake (depth ~0.433 >= 0.10)
        }
        assert!(!t.is_dry_land(20, 20));
        assert!(!t.is_safe_inland_land(20, 20));
        for &(cx, cy) in &[(25usize, 25usize), (26, 25), (25, 26), (26, 26)] {
            t.ocean[cx + cy * stride] = 132; // dry inland bank (depth ~0.031 < 0.10)
        }
        assert!(t.is_dry_land(25, 25));
        assert!(t.is_safe_inland_land(25, 25));
        let logs = srv.state.db.list_audit_logs(None, None, 20).unwrap();
        assert!(!logs.is_empty(), "Audit logs must record server & player events");

        // 5. Domain-Protected Building Durability (/4 EstateGridSize + active lease check):
        // Declare a domain covering cell (10, 10) -> tiles (40..44, 40..44)
        srv.state
            .db
            .upsert_estate(&crate::db::EstateRecord {
                estate_id: "est-1".into(),
                owner_type: 1,
                owner_id: eid.clone(),
                region_id: "pe10gr_1".into(),
                anchor_x: 41,
                anchor_y: 41,
                activated_at: 100.0,
                expires_at: 100_000.0,
                access_for_others: 0,
                cells: vec![(10, 10)],
            })
            .unwrap();
        let in_domain = crate::db::ArtifactRecord {
            entity_id: "hut-in-domain".into(),
            region_id: "pe10gr_1".into(),
            entity_type: moss_spec.entity_type,
            blueprint_id: "s02_shelter_01".into(),
            tile_x: 41,
            tile_y: 41,
            size_x: 2,
            size_y: 2,
            height: 1,
            rotation: 0,
            building_state: BUILDING_STATE_COMPLETED,
            founder_entity_id: eid.clone(),
            level: 1,
            created_at: 100.0,
            durability: 100.0,
            max_durability: 100.0,
            last_decay_at: 100.0,
            gate_opened: false,
            is_lit: false,
            materials_json: "{}".into(),
        };
        let in_wild = crate::db::ArtifactRecord {
            entity_id: "hut-in-wild".into(),
            tile_x: 120,
            tile_y: 120,
            ..in_domain.clone()
        };
        srv.state.db.upsert_artifact(&in_domain).unwrap();
        srv.state.db.upsert_artifact(&in_wild).unwrap();

        // Simulate 10 hours passing at 2.0 durability/hr
        let decayed = srv
            .state
            .db
            .decay_wilderness_artifacts("pe10gr_1", 100.0 + 36_000.0, 2.0)
            .unwrap();
        assert_eq!(decayed.len(), 1);
        assert_eq!(decayed[0].entity_id, "hut-in-wild");
        assert!((decayed[0].durability - 80.0).abs() < 0.1);
        // Domain hut stays 100% untouched!
        let domain_after = srv.state.db.get_artifact("hut-in-domain").unwrap().unwrap();
        assert!((domain_after.durability - 100.0).abs() < 0.01);
        // Expired estate (> expires_at) no longer protects from decay!
        let decayed_after_expiry = srv
            .state
            .db
            .decay_wilderness_artifacts("pe10gr_1", 100_000.0 + 36_000.0, 2.0)
            .unwrap();
        assert!(decayed_after_expiry.iter().any(|a| a.entity_id == "hut-in-domain"));

        // Verify Harbor (7001 / "dock") default_look uses "common" -> "dock_01_wood",
        // WorkbenchTags are loaded, TC_TOUCH on Harbor returns SailingRoutes (303) + SailingRandomPersonal (310)
        // and NEVER DestructArtifact (103), and TC_SET_CHUNK re-streams AppearArtifact after walking away!
        let dock_parts = srv.state.catalog.artifact_default_parts(7001, "dock");
        assert_eq!(dock_parts.get("common").map(String::as_str), Some("dock_01_wood"));
        let bonfire_tags = srv.state.catalog.artifact_workbench_tags(6125, "bonfire");
        assert!(bonfire_tags.iter().any(|(t, _)| t == "fireplace"));

        let port_id = "poi-port-pe10gr_1-63-71";
        c.write_all(
            &encode_frame(
                1.4,
                5,
                0,
                TC_TOUCH,
                &TouchMsg {
                    entity_id: port_id.into(),
                    tile_x: 63,
                    tile_y: 71,
                    entity_type: 7001,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut f_touch = read_one_frame(&mut c, &mut buf).await;
        if f_touch.header.type_code == durango_wire::TC_STATUS_EFFECTS {
            f_touch = read_one_frame(&mut c, &mut buf).await;
        }
        assert_eq!(f_touch.header.type_code, TC_TOUCHED);
        let touched_port = TouchedMsg::unpack(&f_touch.msgpack).unwrap();
        assert!(
            touched_port
                .interactions
                .contains(&durango_wire::INTERACTION_SAILING_ROUTES),
            "Harbor touch must include SailingRoutes (303): {:?}",
            touched_port.interactions
        );
        assert!(
            touched_port
                .interactions
                .contains(&durango_wire::INTERACTION_SAILING_RANDOM_PERSONAL),
            "Harbor touch must include SailingRandomPersonal (310)"
        );
        assert!(
            !touched_port
                .interactions
                .contains(&durango_wire::INTERACTION_DESTRUCT_ARTIFACT),
            "Harbor POI must never include DestructArtifact (103)"
        );

        // Walk 3 chunks away from Harbor (chunk (3,4) -> chunk (7,7)), then walk back to chunk (3,4):
        // Server must re-stream TC_APPEAR_ARTIFACT for the Harbor Dock!
        c.write_all(
            &encode_frame(1.5, 6, 0, TC_SET_CHUNK, &SetChunk { x: 7, y: 7 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        c.write_all(
            &encode_frame(1.6, 7, 0, TC_SET_CHUNK, &SetChunk { x: 3, y: 4 }.pack().unwrap())
                .unwrap(),
        )
        .await
        .unwrap();
        let mut restreamed_harbor = false;
        for _ in 0..23 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_APPEAR_ARTIFACT {
                let app = durango_wire::AppearArtifactMsg::unpack(&f.msgpack).unwrap();
                if app.entity_id == port_id {
                    restreamed_harbor = true;
                    break;
                }
            }
        }
        assert!(
            restreamed_harbor,
            "TC_SET_CHUNK must re-stream Harbor AppearArtifact when returning to chunk (3, 4)"
        );

        // Verify SearchPOIs (904) -> SearchedPOIs (905) and UI queries (GetSocial, GetMissions, GetEncyclopedia)
        // reply immediately on matching reply_of without stalling the Unity UI queue!
        c.write_all(
            &encode_frame(
                1.7,
                8,
                0,
                durango_wire::TC_SEARCH_POIS,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_searched = false;
        for _ in 0..8 {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.type_code == durango_wire::TC_SEARCHED_POIS && f.header.reply_of == 8 {
                saw_searched = true;
                break;
            }
        }
        assert!(saw_searched, "TC_SEARCH_POIS must reply with TC_SEARCHED_POIS");

        c.write_all(
            &encode_frame(
                1.8,
                9,
                0,
                durango_wire::TC_GET_SOCIAL,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_soc = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_soc.header.type_code, durango_wire::TC_SOCIAL);
        assert_eq!(f_soc.header.reply_of, 9);

        c.write_all(
            &encode_frame(
                1.9,
                10,
                0,
                durango_wire::TC_GET_MISSIONS,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let f_mis = read_one_frame(&mut c, &mut buf).await;
        assert_eq!(f_mis.header.type_code, durango_wire::TC_MISSION_INFOS);
        assert_eq!(f_mis.header.reply_of, 10);

        srv.abort();
        let _ = std::fs::remove_file(&db_file);
    }

    #[tokio::test]
    async fn regression_audit_security_catalog_estates_and_sessions() {
        let dir = std::env::temp_dir().join(format!("durango_reg_{}", uuid::Uuid::new_v4()));
        std::fs::create_dir_all(&dir).unwrap();
        let db_path = dir.join("reg.db");

        // 1. Legacy <28-char owner_key purge on startup (Option A)
        {
            let db = crate::db::Database::open(Some(&db_path)).unwrap();
            let conn = db.lock_conn();
            conn.execute(
                "INSERT INTO characters (entity_id, owner_key, name, region_id, terrain_id)
                 VALUES ('legacy-eid', 'usr_ef72cdc18325', 'LegacyGuy', 'tamed-grassland-lv10-01', 'pe10gr_1')",
                [],
            )
            .unwrap();
        }
        {
            let db2 = crate::db::Database::open(Some(&db_path)).unwrap();
            assert!(
                db2.get_by_entity_id("legacy-eid").unwrap().is_none(),
                "legacy <28-char owner_key character must be purged on startup"
            );
        }

        // 2. Catalog formula & tool_stone_knife tag ordering
        let cat = crate::catalog::GameCatalog::load(None);
        let tags = cat.prototype_tags("tool_stone_knife");
        assert!(tags.iter().any(|(t, _)| t == "knife"));
        assert!(tags.iter().any(|(t, _)| t == "tool"));
        assert!(!cat.item_performances("tool_stone_knife", 10).is_empty());

        // 3. Estate overlap rejection (no silent cell theft)
        let db = crate::db::Database::open(None).unwrap();
        let e1 = crate::db::EstateRecord {
            estate_id: "est-a".into(),
            owner_type: 1,
            owner_id: "owner-a".into(),
            region_id: "tamed-grassland-lv10-01".into(),
            anchor_x: 20,
            anchor_y: 20,
            activated_at: 10.0,
            expires_at: 1000.0,
            access_for_others: 1,
            cells: vec![(5, 5)],
        };
        let e2 = crate::db::EstateRecord {
            estate_id: "est-b".into(),
            owner_type: 1,
            owner_id: "owner-b".into(),
            region_id: "tamed-grassland-lv10-01".into(),
            anchor_x: 20,
            anchor_y: 20,
            activated_at: 10.0,
            expires_at: 1000.0,
            access_for_others: 1,
            cells: vec![(5, 5)],
        };
        db.upsert_estate(&e1).unwrap();
        assert!(
            db.upsert_estate(&e2).is_err(),
            "overlapping estate cell must not silently overwrite existing owner"
        );

        // 4. Session SQLite persistence across ServerState restarts
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(db_path.clone()),
            ..ServerConfig::default()
        };
        let tok;
        let eid;
        {
            let st1 = ServerState::new(cfg.clone(), 8191).unwrap();
            let owner = crate::state::canonical_account_id_from_secret("persist-secret-01").unwrap();
            let (e, t) = st1.issue_session_with_auth(&owner, true).await;
            let ch = crate::db::CharacterRecord {
                entity_id: e.clone(),
                owner_key: owner.clone(),
                name: "PersistChar".into(),
                job: 0,
                level: 1,
                region_id: crate::state::DEFAULT_REGION_CODE.into(),
                terrain_id: crate::terrain::DEFAULT_TERRAIN_ID.into(),
                pos_x: 63.0,
                pos_y: 71.0,
                disconnected_at: 0.0,
                created_at: 10.0,
                gender: "male".into(),
                model_info: String::new(),
                exp: 0,
                untrained_count: 0,
                life: 100.0,
                stamina: 100.0,
                energy: 100.0,
                fatigue: 0.0,
            };
            st1.db.upsert_character(&ch).unwrap();
            assert!(st1.bind_session_to_entity(&t, &e).await);
            tok = t;
            eid = e;
        }
        {
            let st2 = ServerState::new(cfg, 8191).unwrap();
            let restored = st2.get_valid_session(&tok).await.expect("session should survive restart");
            assert_eq!(restored.entity_id, eid);
        }

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn round2_flush_all_live_clients_preserves_world_units() {
        let dir = std::env::temp_dir()
            .join(format!("durango_r2_flush_{}", uuid::Uuid::new_v4().simple()));
        std::fs::create_dir_all(&dir).unwrap();
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("r2_flush.db")),
            ..ServerConfig::default()
        };
        let state = crate::state::ServerState::new(cfg, 8191).unwrap();
        let eid = uuid::Uuid::new_v4().to_string();
        state
            .db
            .upsert_character(&crate::db::CharacterRecord {
                entity_id: eid.clone(),
                owner_key: "r2-flush-owner".into(),
                name: "FlushHero".into(),
                job: 0,
                level: 1,
                region_id: crate::state::DEFAULT_REGION_CODE.into(),
                terrain_id: crate::terrain::DEFAULT_TERRAIN_ID.into(),
                pos_x: 12600.0,
                pos_y: 14200.0,
                disconnected_at: 0.0,
                created_at: 10.0,
                gender: "male".into(),
                model_info: String::new(),
                exp: 0,
                untrained_count: 0,
                life: 100.0,
                stamina: 100.0,
                energy: 100.0,
                fatigue: 0.0,
            })
            .unwrap();
        let (tx, _rx) = tokio::sync::mpsc::channel(8);
        state.live_clients.write().await.insert(
            eid.clone(),
            crate::state::LiveClient {
                conn_id: 1,
                entity_id: eid.clone(),
                owner_key: "r2-flush-owner".into(),
                peer_ip: "127.0.0.1".into(),
                name: "FlushHero".into(),
                level: 1,
                region_id: crate::state::DEFAULT_REGION_CODE.into(),
                pos_x: 13250.0,
                pos_y: 15400.0,
                life: 88.0,
                max_life: 100.0,
                stamina: 95.0,
                stamina_at: 100.0,
                energy: 91.0,
                fatigue: 12.0,
                is_alive: true,
                is_resting: false,
                rest_updated_at: 0.0,
                last_dodge_at: 0.0,
                last_battle_action_at: 0.0,
                well_rested_until: 0.0,
                warp_generation: 0,
                in_battle: false,
                battle_target_id: None,
                boarding_on: durango_wire::BOARDING_ON_NONE,
                boarding_since: None,
                boarding_until: None,
                vehicle_entity_id: String::new(),
                tx,
                kick_tx: None,
            },
        );

        state.flush_all_live_clients().await;

        let saved = state.db.get_by_entity_id(&eid).unwrap().unwrap();
        assert!((saved.pos_x - 13250.0).abs() < 0.01);
        assert!((saved.pos_y - 15400.0).abs() < 0.01);
        assert!((saved.life - 88.0).abs() < 0.01);
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn round2_appear_artifact_gate_opened_roundtrip() {
        for opened in [false, true] {
            let msg = durango_wire::AppearArtifactMsg {
                entity_id: "gate-1".into(),
                entity_type: 3201,
                is_alive: true,
                tile_x: 64,
                tile_y: 72,
                size_x: 1,
                size_y: 1,
                height: 2,
                rotation: 0,
                building_state: durango_wire::BUILDING_STATE_COMPLETED,
                founder_entity_id: "p1".into(),
                level: 3,
                now_secs: 100.0,
                parts: std::collections::BTreeMap::new(),
                condition: 100,
                durability: 100.0,
                max_durability: 100.0,
                gate_opened: opened,
                tags: Vec::new(),
            };
            assert_eq!(msg.state_msg().gate_opened, opened);
            let packed = msg.pack().unwrap();
            let unpacked = durango_wire::AppearArtifactMsg::unpack(&packed).unwrap();
            assert_eq!(unpacked.gate_opened, opened);
        }
    }

    #[tokio::test]
    async fn round2_delete_last_character_then_create_gives_valid_uuid() {
        let dir = std::env::temp_dir()
            .join(format!("durango_r2_del_{}", uuid::Uuid::new_v4().simple()));
        std::fs::create_dir_all(&dir).unwrap();
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("r2_del.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());

        let rs = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=r2-del-acct-01"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value = serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = s["session_token"].as_str().unwrap().to_string();

        let r1 = app
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=FirstHero&gender=male&job=0"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r1.status(), StatusCode::OK);
        let c1: Value = serde_json::from_slice(&r1.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid1 = c1["entity_id"].as_str().unwrap().to_string();
        assert!(uuid::Uuid::parse_str(&eid1).is_ok());

        let rd = app
            .clone()
            .oneshot(
                Request::delete(format!("/players/{eid1}"))
                    .header(header::AUTHORIZATION, &tok)
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(rd.status(), StatusCode::OK);

        let r2 = app
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=SecondHero&gender=female&job=1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(r2.status(), StatusCode::OK);
        let c2: Value = serde_json::from_slice(&r2.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid2 = c2["entity_id"].as_str().unwrap().to_string();
        assert!(!eid2.is_empty());
        assert_ne!(eid2, eid1);
        assert!(uuid::Uuid::parse_str(&eid2).is_ok());
        assert!(srv.state.db.get_by_entity_id(&eid2).unwrap().is_some());

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn round2_dead_player_rest_and_empty_repair_kit_rejected() {
        let dir = std::env::temp_dir()
            .join(format!("durango_r2_dead_{}", uuid::Uuid::new_v4().simple()));
        std::fs::create_dir_all(&dir).unwrap();
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("r2_dead.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());

        let rs = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=r2-dead-acct"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let s: Value = serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = s["session_token"].as_str().unwrap().to_string();
        let rc = app
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=RestGuard&gender=male&job=0"))
                    .unwrap(),
            )
            .await
            .unwrap();
        let cjson: Value = serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = cjson["entity_id"].as_str().unwrap().to_string();

        srv.state
            .db
            .insert_items(&[crate::db::ItemRecord {
                item_id: "r2-worn-1".into(),
                entity_id: eid.clone(),
                prototype_id: "tool_stone_knife".into(),
                name: "Stone Knife".into(),
                icon: "icon_tool_knife".into(),
                level: 1,
                size: 1,
                durability: 45.0,
                max_durability: 100.0,
                locked: false,
                collectible_id: None,
                generator_id: None,
                created_at: 10.0,
            }])
            .unwrap();

        let mut c = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut buf = Vec::new();
        c.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid.clone(),
                    session_token: tok,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut c, &mut buf).await.header.type_code, TC_WELCOME);
        c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        for _ in 0..19 {
            let _ = read_one_frame(&mut c, &mut buf).await;
        }

        // 1. Dead player cannot TC_REST_ON -> TC_ABORT
        srv.state
            .live_clients
            .write()
            .await
            .get_mut(&eid)
            .unwrap()
            .is_alive = false;
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                TC_REST_ON,
                &durango_wire::EntityTileMsg {
                    entity_id: String::new(),
                    tile_x: 63,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.reply_of == 3 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // 2. Empty kit_item_ids cannot repair item -> TC_ABORT & durability unchanged
        c.write_all(
            &encode_frame(
                1.3,
                4,
                0,
                durango_wire::TC_REPAIR_ITEM,
                &durango_wire::RepairItemMsg {
                    item_id: "r2-worn-1".into(),
                    kit_item_ids: Vec::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut buf).await;
            if f.header.reply_of == 4 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }
        assert_eq!(srv.state.db.get_item(&eid, "r2-worn-1").unwrap().unwrap().durability, 45.0);

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn round2_unauthorized_container_and_warehouse_rejected() {
        let dir = std::env::temp_dir()
            .join(format!("durango_r2_cont_{}", uuid::Uuid::new_v4().simple()));
        std::fs::create_dir_all(&dir).unwrap();
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("r2_cont.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());
        let now = crate::state::ServerState::now_unix_secs();

        let mk = |acct: &str, name: &str| {
            let a = app.clone();
            let acct = acct.to_string();
            let name = name.to_string();
            async move {
                let rs = a.clone().oneshot(Request::post("/sessions").body(Body::from(format!("account_id={acct}"))).unwrap()).await.unwrap();
                let s: Value = serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
                let tok = s["session_token"].as_str().unwrap().to_string();
                let rc = a
                    .oneshot(
                        Request::post("/players")
                            .header(header::AUTHORIZATION, &tok)
                            .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                            .body(Body::from(format!("name={name}&gender=male&job=0")))
                            .unwrap(),
                    )
                    .await
                    .unwrap();
                let c: Value = serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
                (c["entity_id"].as_str().unwrap().to_string(), tok)
            }
        };
        let (eid_a, _) = mk("r2-owner-a", "OwnerA").await;
        let (eid_b, tok_b) = mk("r2-intruder-b", "IntruderB").await;
        let region_id = srv.state.db.get_by_entity_id(&eid_b).unwrap().unwrap().region_id;

        srv.state
            .db
            .upsert_estate(&crate::db::EstateRecord {
                estate_id: "est-priv-1".into(),
                owner_type: durango_wire::OWNER_TYPE_PLAYER,
                owner_id: eid_a.clone(),
                region_id: region_id.clone(),
                anchor_x: 66,
                anchor_y: 72,
                activated_at: now - 10.0,
                expires_at: now + 3600.0,
                access_for_others: 0,
                cells: vec![(65 / 4, 71 / 4)],
            })
            .unwrap();
        srv.state
            .db
            .upsert_artifact(&crate::db::ArtifactRecord {
                entity_id: "priv-box-1".into(),
                region_id: region_id.clone(),
                entity_type: 3102,
                blueprint_id: "bamboo_basket".into(),
                tile_x: 65,
                tile_y: 71,
                size_x: 1,
                size_y: 1,
                height: 1,
                rotation: 0,
                building_state: durango_wire::BUILDING_STATE_COMPLETED,
                founder_entity_id: eid_a,
                level: 1,
                created_at: now,
                durability: 100.0,
                max_durability: 100.0,
                last_decay_at: now,
                gate_opened: false,
                is_lit: false,
                materials_json: "{}".into(),
            })
            .unwrap();
        srv.state
            .db
            .upsert_artifact(&crate::db::ArtifactRecord {
                entity_id: "wip-box-1".into(),
                region_id,
                entity_type: 3102,
                blueprint_id: "bamboo_basket".into(),
                tile_x: 64,
                tile_y: 71,
                size_x: 1,
                size_y: 1,
                height: 1,
                rotation: 0,
                building_state: durango_wire::BUILDING_STATE_OCCUPIED,
                founder_entity_id: eid_b.clone(),
                level: 1,
                created_at: now,
                durability: 100.0,
                max_durability: 100.0,
                last_decay_at: now,
                gate_opened: false,
                is_lit: false,
                materials_json: "{}".into(),
            })
            .unwrap();
        srv.state
            .db
            .insert_items(&[crate::db::ItemRecord {
                item_id: "b-item-1".into(),
                entity_id: eid_b.clone(),
                prototype_id: "wood_log".into(),
                name: "Log".into(),
                icon: "icon_nat_wood_log".into(),
                level: 1,
                size: 1,
                durability: 100.0,
                max_durability: 100.0,
                locked: false,
                collectible_id: None,
                generator_id: None,
                created_at: now,
            }])
            .unwrap();

        let mut cb = TcpStream::connect(srv.tcp_addr).await.unwrap();
        let mut bb = Vec::new();
        cb.write_all(
            &encode_frame(
                1.0,
                1,
                0,
                TC_AUTH,
                &Auth {
                    entity_id: eid_b,
                    session_token: tok_b,
                    client_version: "3.3.6".into(),
                    device_model: "Linux".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        assert_eq!(read_one_frame(&mut cb, &mut bb).await.header.type_code, TC_WELCOME);
        cb.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
            .await
            .unwrap();
        for _ in 0..19 {
            let _ = read_one_frame(&mut cb, &mut bb).await;
        }

        // Private estate container PutInItem -> TC_ABORT
        cb.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                durango_wire::TC_PUT_IN_ITEM,
                &durango_wire::ContainerItemsOpMsg {
                    entity_id: "priv-box-1".into(),
                    tile_x: 65,
                    tile_y: 71,
                    item_ids: vec!["b-item-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut cb, &mut bb).await;
            if f.header.reply_of == 3 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // Private estate GetWarehouse -> TC_ABORT
        cb.write_all(
            &encode_frame(
                1.3,
                4,
                0,
                durango_wire::TC_GET_WAREHOUSE,
                &durango_wire::EntityTileMsg {
                    entity_id: "priv-box-1".into(),
                    tile_x: 65,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut cb, &mut bb).await;
            if f.header.reply_of == 4 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // Incomplete container PutInItem -> TC_ABORT
        cb.write_all(
            &encode_frame(
                1.4,
                5,
                0,
                durango_wire::TC_PUT_IN_ITEM,
                &durango_wire::ContainerItemsOpMsg {
                    entity_id: "wip-box-1".into(),
                    tile_x: 64,
                    tile_y: 71,
                    item_ids: vec!["b-item-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut cb, &mut bb).await;
            if f.header.reply_of == 5 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn round2_self_buy_market_and_unclaimed_mail_delete_protected() {
        let dir = std::env::temp_dir()
            .join(format!("durango_r2_econ_{}", uuid::Uuid::new_v4().simple()));
        std::fs::create_dir_all(&dir).unwrap();
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("r2_econ.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());
        let now = crate::state::ServerState::now_unix_secs();

        let mk = |acct: &str, name: &str| {
            let a = app.clone();
            let acct = acct.to_string();
            let name = name.to_string();
            async move {
                let rs = a.clone().oneshot(Request::post("/sessions").body(Body::from(format!("account_id={acct}"))).unwrap()).await.unwrap();
                let s: Value = serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
                let tok = s["session_token"].as_str().unwrap().to_string();
                let rc = a
                    .oneshot(
                        Request::post("/players")
                            .header(header::AUTHORIZATION, &tok)
                            .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                            .body(Body::from(format!("name={name}&gender=male&job=0")))
                            .unwrap(),
                    )
                    .await
                    .unwrap();
                let c: Value = serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
                (c["entity_id"].as_str().unwrap().to_string(), tok)
            }
        };
        let (eid_a, tok_a) = mk("r2-econ-a", "SellerSelf").await;
        let (eid_b, tok_b) = mk("r2-econ-b", "SenderMail").await;

        srv.state
            .db
            .insert_items(&[
                crate::db::ItemRecord {
                    item_id: "self-mkt-1".into(),
                    entity_id: eid_a.clone(),
                    prototype_id: "wood_log".into(),
                    name: "SelfLog".into(),
                    icon: "icon_nat_wood_log".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
                crate::db::ItemRecord {
                    item_id: "mail-att-1".into(),
                    entity_id: eid_b.clone(),
                    prototype_id: "wood_log".into(),
                    name: "GiftLog".into(),
                    icon: "icon_nat_wood_log".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
            ])
            .unwrap();

        let connect = |eid: String, tok: String, addr: std::net::SocketAddr| async move {
            let mut c = TcpStream::connect(addr).await.unwrap();
            let mut b = Vec::new();
            c.write_all(
                &encode_frame(
                    1.0,
                    1,
                    0,
                    TC_AUTH,
                    &Auth {
                        entity_id: eid,
                        session_token: tok,
                        client_version: "3.3.6".into(),
                        device_model: "Linux".into(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            assert_eq!(read_one_frame(&mut c, &mut b).await.header.type_code, TC_WELCOME);
            c.write_all(&encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap())
                .await
                .unwrap();
            for _ in 0..19 {
                let _ = read_one_frame(&mut c, &mut b).await;
            }
            (c, b)
        };
        let (mut ca, mut ba) = connect(eid_a.clone(), tok_a, srv.tcp_addr).await;
        let (mut cb, mut bb) = connect(eid_b, tok_b, srv.tcp_addr).await;

        // Seller A lists item, searches it, tries to buy own listing -> TC_ABORT
        ca.write_all(
            &encode_frame(
                1.5,
                3,
                0,
                TC_REGISTER_PRODUCT,
                &RegisterProductMsg {
                    item_id: "self-mkt-1".into(),
                    price: 100,
                    duration: 24.0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut ca, &mut ba).await;
            if f.header.reply_of == 3 {
                assert_eq!(f.header.type_code, TC_OK);
                break;
            }
        }
        ca.write_all(
            &encode_frame(
                1.6,
                4,
                0,
                TC_SEARCH_PRODUCTS,
                &SearchProductsMsg {
                    item_name: Some("SelfLog".into()),
                    prototype_id: None,
                    skip: 0,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let pid = loop {
            let f = read_one_frame(&mut ca, &mut ba).await;
            if f.header.reply_of == 4 && f.header.type_code == TC_PRODUCTS {
                break ProductsMsg::unpack(&f.msgpack).unwrap().products[0].id.clone();
            }
        };
        ca.write_all(
            &encode_frame(
                1.7,
                5,
                0,
                TC_BUY_PRODUCT,
                &BuyProductMsg {
                    product_id: pid,
                    payment_ids: Vec::new(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut ca, &mut ba).await;
            if f.header.reply_of == 5 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // Player B sends mail with attachment -> Player A tries TC_DELETE_USER_MAILS before accepting -> protected!
        cb.write_all(
            &encode_frame(
                1.8,
                3,
                0,
                durango_wire::TC_SEND_MAIL,
                &durango_wire::SendMailMsg {
                    recipient_id: eid_a.clone(),
                    text: "Do not accidentally delete!".into(),
                    item_ids: vec!["mail-att-1".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut cb, &mut bb).await;
            if f.header.reply_of == 3 {
                assert_eq!(f.header.type_code, TC_OK);
                break;
            }
        }
        let mail_id = srv.state.db.list_mails(&eid_a).unwrap()[0].mail_id.clone();
        ca.write_all(
            &encode_frame(
                1.9,
                6,
                0,
                durango_wire::TC_DELETE_USER_MAILS,
                &durango_wire::MailIdsMsg {
                    mail_ids: vec![mail_id.clone()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut ca, &mut ba).await;
            if f.header.reply_of == 6 {
                assert_eq!(f.header.type_code, TC_OK);
                break;
            }
        }
        assert_eq!(srv.state.db.list_mails(&eid_a).unwrap().len(), 1);
        assert_eq!(srv.state.db.list_mails(&eid_a).unwrap()[0].mail_id, mail_id);

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn test_bugfix_regression_suite() {
        use axum::body::Body;
        use axum::http::{header, Request, StatusCode};
        use http_body_util::BodyExt;
        use serde_json::Value;
        use tokio::io::AsyncWriteExt;
        use tokio::net::TcpStream;
        use tower::ServiceExt;

        let dir = std::env::temp_dir().join(format!(
            "durango_bugfixes_{}",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("bugfixes.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());
        let now = crate::state::ServerState::now_unix_secs();

        // 1. HTTP: /season/pass & /season/claim with non-empty POST bodies + auth on cancel_deletion + personal_region_id
        let rs = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=acct-fix-1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(rs.status(), StatusCode::OK);
        let sess_json: Value =
            serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = sess_json["session_token"].as_str().unwrap().to_string();

        let rc = app
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=FixTester&gender=male&job=0"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(rc.status(), StatusCode::OK);
        let char_json: Value =
            serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = char_json["entity_id"].as_str().unwrap().to_string();

        let sp_resp = app
            .clone()
            .oneshot(
                Request::post("/season/pass")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("lang=en&season=1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(sp_resp.status(), StatusCode::OK);

        let sc_resp = app
            .clone()
            .oneshot(
                Request::post("/season/claim")
                    .header(header::AUTHORIZATION, &tok)
                    .body(Body::from("reward_id=1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(sc_resp.status(), StatusCode::OK);

        let unauth_cancel = app
            .clone()
            .oneshot(
                Request::post(&format!("/players/{eid}/cancel_player_deletion"))
                    .body(Body::from("reason=test"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(unauth_cancel.status(), StatusCode::UNAUTHORIZED);

        let auth_cancel = app
            .clone()
            .oneshot(
                Request::post(&format!("/players/{eid}/cancel_player_deletion"))
                    .header(header::AUTHORIZATION, &tok)
                    .body(Body::from("reason=test"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(auth_cancel.status(), StatusCode::OK);

        let mut pts = srv.state.db.get_player_points(&eid).unwrap();
        pts.personal_region_id = Some("tamed-grassland-lv10-01".into());
        srv.state
            .db
            .upsert_player_points(&eid, &pts)
            .unwrap();
        let pinfo_resp = app
            .clone()
            .oneshot(
                Request::get(&format!("/players/{eid}"))
                    .header(header::AUTHORIZATION, &tok)
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(pinfo_resp.status(), StatusCode::OK);
        let pinfo: Value = serde_json::from_slice(
            &pinfo_resp.into_body().collect().await.unwrap().to_bytes(),
        )
        .unwrap();
        assert_eq!(
            pinfo["personal_region_id"].as_str(),
            Some("tamed-grassland-lv10-01")
        );

        // Seed items & T-Stone for TCP tests
        srv.state.db.credit_t_stone(&eid, 50).unwrap();
        srv.state
            .db
            .insert_items(&[
                crate::db::ItemRecord {
                    item_id: "fix-pebble-1".into(),
                    entity_id: eid.clone(),
                    prototype_id: "stone_pebble".into(),
                    name: "Pebble".into(),
                    icon: "icon_nat_stone_pebble".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
                crate::db::ItemRecord {
                    item_id: "fix-knife-1".into(),
                    entity_id: eid.clone(),
                    prototype_id: "tool_stone_knife".into(),
                    name: "Stone Knife".into(),
                    icon: "icon_tool_stone_knife".into(),
                    level: 1,
                    size: 2,
                    durability: 50.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
                crate::db::ItemRecord {
                    item_id: "fix-locked-log".into(),
                    entity_id: eid.clone(),
                    prototype_id: "wood_log".into(),
                    name: "Log".into(),
                    icon: "icon_nat_wood_log".into(),
                    level: 1,
                    size: 2,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: true,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
            ])
            .unwrap();

        let connect = |eid: String, tok: String, addr: std::net::SocketAddr| async move {
            let mut c = TcpStream::connect(addr).await.unwrap();
            let mut b = Vec::new();
            c.write_all(
                &encode_frame(
                    1.0,
                    1,
                    0,
                    TC_AUTH,
                    &Auth {
                        entity_id: eid,
                        session_token: tok,
                        client_version: "3.3.6".into(),
                        device_model: "Linux".into(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            let wf = read_one_frame(&mut c, &mut b).await;
            assert_eq!(wf.header.type_code, TC_WELCOME);
            let welcome = Welcome::unpack(&wf.msgpack).unwrap();
            c.write_all(
                &encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap(),
            )
            .await
            .unwrap();
            for _ in 0..19 {
                let _ = read_one_frame(&mut c, &mut b).await;
            }
            (c, b, welcome)
        };

        let (mut c, mut b, _w0) = connect(eid.clone(), tok.clone(), srv.tcp_addr).await;

        // 2. TC_SET_STORAGE_ITEM with MsgPack Classic raw/str value (0xa0..0xbf)
        let storage_payload = vec![
            0x92, // fixarray(2)
            0xad, b't', b'u', b't', b'o', b'r', b'i', b'a', b'l', b'_', b'm', b'e', b'm', b'o',
            0xa4, b'd', b'o', b'n', b'e', // fixstr "done" as Unity Classic binary-as-raw
        ];
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                durango_wire::TC_SET_STORAGE_ITEM,
                &storage_payload,
            )
            .unwrap(),
        )
        .await
        .unwrap();

        // 3. TC_EQUIP rejects non-equippable item & wrong slot
        c.write_all(
            &encode_frame(
                1.3,
                4,
                0,
                durango_wire::TC_EQUIP,
                &EquipMsg {
                    slot_name: "weapon".into(),
                    slot_type: EQUIP_SLOT_1,
                    item_id: "fix-locked-log".into(),
                    action: "equip".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 4 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }
        c.write_all(
            &encode_frame(
                1.4,
                5,
                0,
                durango_wire::TC_EQUIP,
                &EquipMsg {
                    slot_name: "body".into(),
                    slot_type: EQUIP_SLOT_1,
                    item_id: "fix-knife-1".into(),
                    action: "equip".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 5 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // 4. TC_SEND_MAIL with locked/missing attachment -> TC_ABORT + no stripped mail created
        c.write_all(
            &encode_frame(
                1.5,
                6,
                0,
                durango_wire::TC_SEND_MAIL,
                &durango_wire::SendMailMsg {
                    recipient_id: eid.clone(),
                    text: "Should abort".into(),
                    item_ids: vec!["fix-locked-log".into()],
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 6 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }
        assert!(srv.state.db.list_mails(&eid).unwrap().is_empty());

        // 5. TC_GET_MAILS returns mails
        c.write_all(
            &encode_frame(
                1.6,
                7,
                0,
                durango_wire::TC_GET_MAILS,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 7 {
                assert_eq!(f.header.type_code, durango_wire::TC_MAILS);
                break;
            }
        }

        // 6. TC_CANCEL_CRAFTING cancels in-flight craft -> TC_CANCELED, keeps fix-pebble-1
        let mut cancel_craft_mats = std::collections::BTreeMap::new();
        cancel_craft_mats.insert("material".into(), vec!["fix-pebble-1".into()]);
        c.write_all(
            &encode_frame(
                1.7,
                8,
                0,
                durango_wire::TC_CRAFT,
                &CraftMsg {
                    recipe_id: "stone_knife".into(),
                    materials: cancel_craft_mats,
                    tool_item_id: None,
                    reform_slot_index: None,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_TIMER && f.header.reply_of == 8 {
                break;
            }
        }
        let cancel_payload = vec![
            0x93, 0xa0, // entity_id ""
            0x92, 0x00, 0x00, // [0, 0]
            0xa0, // crafting_id ""
        ];
        c.write_all(
            &encode_frame(
                1.8,
                9,
                0,
                durango_wire::TC_CANCEL_CRAFTING,
                &cancel_payload,
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_canceled = false;
        for _ in 0..10 {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == durango_wire::TC_CANCELED {
                saw_canceled = true;
                break;
            }
        }
        assert!(saw_canceled, "expected TC_CANCELED after TC_CANCEL_CRAFTING");
        assert!(
            srv.state
                .db
                .list_items(&eid)
                .unwrap()
                .iter()
                .any(|it| it.item_id == "fix-pebble-1"),
            "canceled craft must not consume input items"
        );

        // 7. Estate shrink connectivity (reject splitting middle cell with TC_ABORT)
        let dec_buf = durango_wire::DeclareEstateMsg {
            owner_type: durango_wire::OWNER_TYPE_PERSONAL_PLAYER,
            cell_x: 19,
            cell_y: 17,
        }
        .pack()
        .unwrap();
        c.write_all(
            &encode_frame(1.9, 10, 0, durango_wire::TC_DECLARE_ESTATE, &dec_buf).unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 10 {
                assert_eq!(f.header.type_code, durango_wire::TC_ESTATE_LICENSE);
                break;
            }
        }
        let estate_id = srv.state.db.list_estates_by_owner(&eid).unwrap()[0]
            .estate_id
            .clone();
        let pack_cell_op = |eid_str: &str, cx: u8, cy: u8| {
            let mut out = Vec::with_capacity(eid_str.len() + 8);
            out.push(0x92);
            if eid_str.len() <= 31 {
                out.push(0xa0 | (eid_str.len() as u8));
            } else {
                out.push(0xd9);
                out.push(eid_str.len() as u8);
            }
            out.extend_from_slice(eid_str.as_bytes());
            out.push(0x92);
            out.push(cx);
            out.push(cy);
            out
        };
        for (seq, cx) in [(11u32, 20u8), (12u32, 21u8)] {
            c.write_all(
                &encode_frame(
                    2.0,
                    seq,
                    0,
                    durango_wire::TC_EXPAND_ESTATE,
                    &pack_cell_op(&estate_id, cx, 17),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            loop {
                let f = read_one_frame(&mut c, &mut b).await;
                if f.header.reply_of == seq {
                    assert_eq!(f.header.type_code, durango_wire::TC_ESTATE_LICENSE);
                    break;
                }
            }
        }
        // Shrinking middle (20, 17) splits (19,17) and (21,17) -> must TC_ABORT
        c.write_all(
            &encode_frame(
                2.1,
                13,
                0,
                durango_wire::TC_SHRINK_ESTATE,
                &pack_cell_op(&estate_id, 20, 17),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 13 {
                assert_eq!(f.header.type_code, durango_wire::TC_ABORT);
                break;
            }
        }

        // 8. TC_REPAIR_IMMEDIATE spends T-Stone + emits TC_WALLET_UPDATED
        srv.state
            .db
            .upsert_artifact(&crate::db::ArtifactRecord {
                entity_id: "art-rep-1".into(),
                region_id: "tamed-grassland-lv10-01".into(),
                entity_type: 6151,
                blueprint_id: "workbench".into(),
                tile_x: 63,
                tile_y: 71,
                size_x: 1,
                size_y: 1,
                height: 0,
                rotation: 0,
                building_state: BUILDING_STATE_COMPLETED,
                founder_entity_id: eid.clone(),
                level: 1,
                created_at: now,
                durability: 40.0,
                max_durability: 100.0,
                last_decay_at: now,
                gate_opened: false,
                is_lit: false,
                materials_json: "{}".into(),
            })
            .unwrap();
        let before_repair_ts = srv.state.db.get_t_stone(&eid).unwrap();
        c.write_all(
            &encode_frame(
                2.2,
                14,
                0,
                durango_wire::TC_REPAIR_IMMEDIATE,
                &durango_wire::RepairImmediateMsg {
                    entity_id: "art-rep-1".into(),
                    tile_x: 63,
                    tile_y: 71,
                    cost_amount: 5,
                    cost_currency: 1,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 14 {
                assert_eq!(f.header.type_code, TC_OK);
                break;
            }
        }
        assert_eq!(srv.state.db.get_t_stone(&eid).unwrap(), before_repair_ts - 5);

        // 9. Travel resets live_clients coords so disconnect before next Ready cannot overwrite destination spawn
        c.write_all(
            &encode_frame(
                2.3,
                15,
                0,
                TC_TRAVEL_TO_STABLE_REGION,
                &TravelToStableRegionMsg {
                    region_id: "tamed-tropical-lv10-01".into(),
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_EMIGRATED {
                break;
            }
        }
        {
            let live = srv.state.live_clients.read().await;
            let lc = live.get(&eid).unwrap();
            assert_eq!(lc.region_id, "tamed-tropical-lv10-01");
            assert_eq!((lc.pos_x, lc.pos_y), (0.0, 0.0));
        }
        drop(c);

        // Reconnect verifies Welcome.storage preserved "tutorial_memo" = b"done"
        let (c2, _b2, w2) = connect(eid, tok, srv.tcp_addr).await;
        assert_eq!(
            w2.storage.get("tutorial_memo").map(Vec::as_slice),
            Some(b"done".as_slice())
        );
        drop(c2);

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn test_balloon_get_artifact_and_bone_icon_fixes() {
        use axum::body::Body;
        use axum::http::{header, Request, StatusCode};
        use http_body_util::BodyExt;
        use serde_json::Value;
        use tokio::io::AsyncWriteExt;
        use tokio::net::TcpStream;
        use tower::ServiceExt;

        let dir = std::env::temp_dir().join(format!(
            "durango_balloon_{}",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let cfg = ServerConfig {
            http_bind: "127.0.0.1:0".into(),
            tcp_bind: "127.0.0.1:0".into(),
            db_path: Some(dir.join("balloon.db")),
            wildlife_wander_interval_ms: None,
            ..ServerConfig::default()
        };
        let srv = start_server(cfg).await.unwrap();
        let app = build_http_router(srv.state.clone());
        let now = crate::state::ServerState::now_unix_secs();

        // 1. Verify butcher_generators + default_icon_for bone icon fix
        let butcher = srv.state.catalog.butcher_generators(1);
        let bone_gen = butcher
            .iter()
            .find(|g| g.prototype_id == "bone_leg")
            .expect("butcher_generators should yield bone_leg");
        assert_eq!(bone_gen.icon, "icon_nat_bone");
        assert_eq!(crate::catalog::default_icon_for("bone"), "icon_nat_bone");
        assert_eq!(
            crate::catalog::default_icon_for("bone_leg"),
            "icon_nat_bone"
        );

        let rs = app
            .clone()
            .oneshot(
                Request::post("/sessions")
                    .body(Body::from("account_id=acct-balloon-1"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(rs.status(), StatusCode::OK);
        let sess: Value =
            serde_json::from_slice(&rs.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let tok = sess["session_token"].as_str().unwrap().to_string();

        let rc = app
            .clone()
            .oneshot(
                Request::post("/players")
                    .header(header::AUTHORIZATION, &tok)
                    .header(header::CONTENT_TYPE, "application/x-www-form-urlencoded")
                    .body(Body::from("name=BalloonPilot&gender=male&job=0"))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(rc.status(), StatusCode::OK);
        let ch_json: Value =
            serde_json::from_slice(&rc.into_body().collect().await.unwrap().to_bytes()).unwrap();
        let eid = ch_json["entity_id"].as_str().unwrap().to_string();

        let connect = |eid: String, tok: String, addr: std::net::SocketAddr| async move {
            let mut c = TcpStream::connect(addr).await.unwrap();
            let mut b = Vec::new();
            c.write_all(
                &encode_frame(
                    1.0,
                    1,
                    0,
                    TC_AUTH,
                    &Auth {
                        entity_id: eid,
                        session_token: tok,
                        client_version: "3.3.6".into(),
                        device_model: "Linux".into(),
                    }
                    .pack()
                    .unwrap(),
                )
                .unwrap(),
            )
            .await
            .unwrap();
            let wf = read_one_frame(&mut c, &mut b).await;
            assert_eq!(wf.header.type_code, TC_WELCOME);
            let welcome = Welcome::unpack(&wf.msgpack).unwrap();
            c.write_all(
                &encode_frame(1.1, 2, 0, TC_READY, &pack_empty_struct().unwrap()).unwrap(),
            )
            .await
            .unwrap();
            for _ in 0..19 {
                let _ = read_one_frame(&mut c, &mut b).await;
            }
            (c, b, welcome)
        };

        // 2. Fresh character on personal island spawns hovering inside Hot Air Balloon
        let (mut c, mut b, _) = connect(eid.clone(), tok.clone(), srv.tcp_addr).await;
        {
            let live = srv.state.live_clients.read().await;
            let lc = live.get(&eid).unwrap();
            assert_eq!(lc.boarding_on, durango_wire::BOARDING_ON_AIR_BALLOON);
            assert!(lc.boarding_since.is_some());
            assert!(lc.boarding_until.is_some());
        }

        // 3. TC_UNMOUNT_AIR_BALLOON clears boarding_on to NONE, broadcasts TC_PLAYER_DISPLAY, persists _balloon_landed
        c.write_all(
            &encode_frame(
                1.2,
                3,
                0,
                durango_wire::TC_UNMOUNT_AIR_BALLOON,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_PLAYER_DISPLAY {
                let pd = PlayerDisplayMsg::unpack(&f.msgpack).unwrap();
                assert_eq!(pd.appearance.boarding_on, durango_wire::BOARDING_ON_NONE);
                break;
            }
        }
        assert!(srv
            .state
            .db
            .list_player_storage(&eid)
            .unwrap()
            .contains_key("_balloon_landed"));

        // 4. TC_MOUNT_AIR_BALLOON re-boards balloon, broadcasts TC_PLAYER_DISPLAY + TC_OK
        c.write_all(
            &encode_frame(
                1.3,
                4,
                0,
                durango_wire::TC_MOUNT_AIR_BALLOON,
                &durango_wire::MountAirBalloonMsg { with_voucher: None }
                    .pack()
                    .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        let mut saw_disp = false;
        let mut saw_ok = false;
        while !(saw_disp && saw_ok) {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_PLAYER_DISPLAY {
                let pd = PlayerDisplayMsg::unpack(&f.msgpack).unwrap();
                assert_eq!(
                    pd.appearance.boarding_on,
                    durango_wire::BOARDING_ON_AIR_BALLOON
                );
                saw_disp = true;
            } else if f.header.reply_of == 4 {
                assert_eq!(f.header.type_code, TC_OK);
                saw_ok = true;
            }
        }

        // Unmount again and test /balloon chat command
        c.write_all(
            &encode_frame(
                1.35,
                5,
                0,
                durango_wire::TC_UNMOUNT_AIR_BALLOON,
                &pack_empty_struct().unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_PLAYER_DISPLAY {
                break;
            }
        }
        c.write_all(
            &encode_frame(
                1.4,
                6,
                0,
                TC_SAY_IN_EXCLUSIVE_CHANNEL,
                &SayInExclusiveChannelMsg {
                    entity_id: eid.clone(),
                    channel_type: 0,
                    text: "/balloon".into(),
                    speaker_name: None,
                    speaker_freq: 0,
                    time: 1.4,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.type_code == TC_PLAYER_DISPLAY {
                let pd = PlayerDisplayMsg::unpack(&f.msgpack).unwrap();
                assert_eq!(
                    pd.appearance.boarding_on,
                    durango_wire::BOARDING_ON_AIR_BALLOON
                );
                break;
            }
        }

        // 5. TC_GET_ARTIFACT returns TC_ARTIFACT_MATERIALS with reply_of == seq (opens Construction screen)
        srv.state
            .db
            .insert_items(&[crate::db::ItemRecord {
                item_id: "mat-log-1".into(),
                entity_id: eid.clone(),
                prototype_id: "wood_log".into(),
                name: "Log".into(),
                icon: "icon_nat_wood_log".into(),
                level: 1,
                size: 2,
                durability: 100.0,
                max_durability: 100.0,
                locked: false,
                collectible_id: None,
                generator_id: None,
                created_at: now,
            }])
            .unwrap();
        srv.state
            .db
            .upsert_artifact(&crate::db::ArtifactRecord {
                entity_id: "sign-site-1".into(),
                region_id: "tamed-grassland-lv10-01".into(),
                entity_type: 6151,
                blueprint_id: "board_message_01".into(),
                tile_x: 63,
                tile_y: 71,
                size_x: 1,
                size_y: 1,
                height: 0,
                rotation: 0,
                building_state: durango_wire::BUILDING_STATE_OCCUPIED,
                founder_entity_id: eid.clone(),
                level: 1,
                created_at: now,
                durability: 100.0,
                max_durability: 100.0,
                last_decay_at: now,
                gate_opened: false,
                is_lit: false,
                materials_json: "{}".into(),
            })
            .unwrap();
        // Pack PutMaterialsIntoArtifactMsg: ["sign-site-1", [63, 71], {"pillar": ["mat-log-1"]}]
        let put_payload = vec![
            0x93, // array(3)
            0xab, b's', b'i', b'g', b'n', b'-', b's', b'i', b't', b'e', b'-', b'1', // "sign-site-1"
            0x92, 63, 71, // [63, 71]
            0x81, // map(1)
            0xa6, b'p', b'i', b'l', b'l', b'a', b'r', // "pillar"
            0x91, // array(1)
            0xa9, b'm', b'a', b't', b'-', b'l', b'o', b'g', b'-', b'1', // "mat-log-1"
        ];
        c.write_all(
            &encode_frame(
                1.5,
                7,
                0,
                durango_wire::TC_PUT_MATERIALS_INTO_ARTIFACT,
                &put_payload,
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 7 {
                assert_eq!(f.header.type_code, TC_OK);
                break;
            }
        }
        c.write_all(
            &encode_frame(
                1.6,
                8,
                0,
                durango_wire::TC_GET_ARTIFACT,
                &durango_wire::EntityTileMsg {
                    entity_id: "sign-site-1".into(),
                    tile_x: 63,
                    tile_y: 71,
                }
                .pack()
                .unwrap(),
            )
            .unwrap(),
        )
        .await
        .unwrap();
        loop {
            let f = read_one_frame(&mut c, &mut b).await;
            if f.header.reply_of == 8 {
                assert_eq!(f.header.type_code, durango_wire::TC_ARTIFACT_MATERIALS);
                assert!(!f.msgpack.is_empty());
                break;
            }
        }
        drop(c);

        // 6. Reconnect after _balloon_landed -> spawns on foot (BOARDING_ON_NONE)
        let (c2, _b2, w2) = connect(eid.clone(), tok, srv.tcp_addr).await;
        assert!(w2.storage.contains_key("_balloon_landed"));
        {
            let live = srv.state.live_clients.read().await;
            assert_eq!(
                live.get(&eid).unwrap().boarding_on,
                durango_wire::BOARDING_ON_NONE
            );
        }
        drop(c2);

        srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_find_nearest_dry_tile_skips_harbor_water() {
        let dir = std::env::temp_dir().join(format!(
            "durango-dry-spawn-test-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        let _ = std::fs::create_dir_all(&dir);
        let zip_path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../_backup_2026-11-19/workspace-artifacts/build/server/data/terrains/pe10gr_1.zip");
        if zip_path.exists() {
            let terrain = terrain::LoadedTerrain::load_from_zip("pe10gr_1", &zip_path)
                .expect("load pe10gr_1.zip");
            let dry = terrain.find_nearest_dry_tile(terrain.entry_tile, 32);
            assert_ne!(dry, terrain.entry_tile);
            assert!(terrain.is_safe_inland_land(dry.0, dry.1));
        }
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_default_icon_and_db_icon_migrations() {
        assert_eq!(catalog::default_icon_for("stone"), "icon_nat_mine_stone");
        assert_eq!(catalog::default_icon_for("wildberry"), "icon_nat_fruit_wildberry");
        assert_eq!(catalog::default_icon_for("bone_leg"), "icon_nat_bone");
        assert_eq!(catalog::default_icon_for("tool_stone_knife"), "blade_stone");

        let dir = std::env::temp_dir().join(format!(
            "durango-icon-mig-test-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir_all(&dir).unwrap();
        let db_path = dir.join("test.db");

        {
            let db = db::Database::open(Some(&db_path)).unwrap();
            let now = 1000.0;
            db.upsert_character(&db::CharacterRecord {
                entity_id: "ent-icon-1".into(),
                owner_key: "usr_0123456789abcdef01234567".into(),
                name: "IconTester".into(),
                job: 7,
                level: 1,
                region_id: "tamed-grassland-lv10-01".into(),
                terrain_id: "pe10gr_1".into(),
                pos_x: 14400.0,
                pos_y: 14200.0,
                disconnected_at: 0.0,
                created_at: now,
                gender: "male".into(),
                model_info: "{}".into(),
                exp: 0,
                untrained_count: 0,
                life: 100.0,
                stamina: 100.0,
                energy: 100.0,
                fatigue: 0.0,
            })
            .unwrap();
            db.insert_items(&[
                db::ItemRecord {
                    item_id: "it-stone".into(),
                    entity_id: "ent-icon-1".into(),
                    prototype_id: "stone_pebble".into(),
                    name: "Pebble".into(),
                    icon: "icon_nat_stone".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
                db::ItemRecord {
                    item_id: "it-berry".into(),
                    entity_id: "ent-icon-1".into(),
                    prototype_id: "fruit_berry".into(),
                    name: "Berry".into(),
                    icon: "icon_nat_fruit".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
                db::ItemRecord {
                    item_id: "it-knife".into(),
                    entity_id: "ent-icon-1".into(),
                    prototype_id: "knife_basic".into(),
                    name: "Knife".into(),
                    icon: "icon_tool_knife".into(),
                    level: 1,
                    size: 1,
                    durability: 100.0,
                    max_durability: 100.0,
                    locked: false,
                    collectible_id: None,
                    generator_id: None,
                    created_at: now,
                },
            ])
            .unwrap();
        }

        // Re-open triggers idempotent icon repair migrations
        let db2 = db::Database::open(Some(&db_path)).unwrap();
        let items = db2.list_items("ent-icon-1").unwrap();
        assert_eq!(items.iter().find(|i| i.item_id == "it-stone").unwrap().icon, "icon_nat_mine_stone");
        assert_eq!(items.iter().find(|i| i.item_id == "it-berry").unwrap().icon, "icon_nat_fruit_wildberry");
        assert_eq!(items.iter().find(|i| i.item_id == "it-knife").unwrap().icon, "blade_stone");

        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn test_upstream_servers_json_sync_merges_local_and_vm() {
        use axum::{body::Body, http::Request};
        use tower::ServiceExt;

        assert_eq!(
            normalize_servers_json_url("203.0.113.10").as_deref(),
            Some("http://203.0.113.10:8190/servers.json")
        );
        assert_eq!(
            normalize_servers_json_url("https://example.com/servers.json").as_deref(),
            Some("https://example.com/servers.json")
        );

        let dir = std::env::temp_dir().join(format!(
            "durango-upstream-merge-test-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir_all(&dir).unwrap();

        // 1. Start mock remote VM server with a trusted server entry in its directory
        let mut vm_cfg = ServerConfig::default();
        vm_cfg.http_bind = "127.0.0.1:0".into();
        vm_cfg.tcp_bind = "127.0.0.1:0".into();
        vm_cfg.server_name = "VM Trusted Server".into();
        vm_cfg.region_tag = "SEA".into();
        vm_cfg.db_path = Some(dir.join("vm.db"));
        vm_cfg.wildlife_wander_interval_ms = None;
        let vm_srv = start_server(vm_cfg).await.expect("start vm srv");
        let vm_url = format!("http://127.0.0.1:{}/servers.json", vm_srv.http_addr.port());

        // Add another active trusted peer into VM's directory
        vm_srv
            .state
            .upsert_directory_server(crate::state::DirectoryServerEntry {
                server_id: "trusted-peer-1".into(),
                name: "Trusted Community Guild".into(),
                gateway: "http://198.51.100.42:8190".into(),
                region: "EU".into(),
                online_players: 7,
                max_players: 100,
                password_required: false,
                pvp_enabled: false,
                verified: Some(true),
                version: "3.3.6".into(),
                last_seen_at: crate::state::ServerState::now_unix_secs(),
            })
            .await;

        // 2. Start local server pointing upstream_servers_urls at the VM's /servers.json
        let mut local_cfg = ServerConfig::default();
        local_cfg.http_bind = "127.0.0.1:0".into();
        local_cfg.tcp_bind = "127.0.0.1:0".into();
        local_cfg.server_name = "Durango-CE Local Server".into();
        local_cfg.db_path = Some(dir.join("local.db"));
        local_cfg.wildlife_wander_interval_ms = None;
        local_cfg.upstream_servers_urls = vec![vm_url.clone()];
        let local_srv = start_server(local_cfg).await.expect("start local srv");

        let router = crate::http::build_http_router(local_srv.state.clone());
        let resp = router
            .oneshot(
                Request::get("/servers.json")
                    .header("host", format!("127.0.0.1:{}", local_srv.http_addr.port()))
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), axum::http::StatusCode::OK);
        let bytes = http_body_util::BodyExt::collect(resp.into_body())
            .await
            .unwrap()
            .to_bytes();
        let json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        let servers = json["servers"].as_array().unwrap();
        let names: Vec<&str> = servers
            .iter()
            .filter_map(|s| s["name"].as_str())
            .collect();
        assert!(names.contains(&"Durango-CE Local Server"), "got {names:?}");
        assert!(names.contains(&"VM Trusted Server"), "got {names:?}");
        assert!(names.contains(&"Trusted Community Guild"), "got {names:?}");

        local_srv.abort();
        vm_srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[tokio::test]
    async fn test_ca_attestation_accepts_genuine_and_rejects_tampered_server() {
        use axum::{body::Body, http::Request};
        use tower::ServiceExt;

        // 1. Cryptographic vectors (NIST SHA-256 + RFC 4231 HMAC-SHA256 Test Case 2)
        assert_eq!(
            crate::attestation::sha256_hex(b"abc"),
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
        );
        assert_eq!(
            crate::attestation::hmac_sha256_hex(b"Jefe", b"what do ya want for nothing?"),
            "5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843"
        );

        let dir = std::env::temp_dir().join(format!(
            "durango-ca-attestation-test-{}-{}",
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        std::fs::create_dir_all(&dir).unwrap();

        let official_key = "test-secret-official-build-key";
        let exe_hash = crate::attestation::compute_self_exe_sha256();
        let assets_digest = crate::attestation::compute_assets_digest(None);

        let allowlist_path = dir.join("trusted-releases.json");
        let allowlist_json = serde_json::json!({
            "require_attestation": true,
            "releases": [{
                "release_id": "test-official-v1",
                "exe_sha256": exe_hash,
                "assets_digest": assets_digest,
                "hmac_key": official_key
            }]
        });
        std::fs::write(&allowlist_path, serde_json::to_vec_pretty(&allowlist_json).unwrap()).unwrap();

        // 2. Start CA Server enforcing attestation allowlist
        let mut ca_cfg = ServerConfig::default();
        ca_cfg.http_bind = "127.0.0.1:0".into();
        ca_cfg.tcp_bind = "127.0.0.1:0".into();
        ca_cfg.server_name = "Official CA".into();
        ca_cfg.db_path = Some(dir.join("ca.db"));
        ca_cfg.wildlife_wander_interval_ms = None;
        ca_cfg.ca_allowlist_path = Some(allowlist_path);
        ca_cfg.ca_require_attestation = true;
        let ca_srv = start_server(ca_cfg).await.expect("start ca");

        // 3. Start Genuine Community Node with matching attestation key
        let mut node_cfg = ServerConfig::default();
        node_cfg.http_bind = "127.0.0.1:0".into();
        node_cfg.tcp_bind = "127.0.0.1:0".into();
        node_cfg.server_name = "Genuine Community Node".into();
        node_cfg.db_path = Some(dir.join("node.db"));
        node_cfg.wildlife_wander_interval_ms = None;
        node_cfg.attestation_key = Some(official_key.into());
        let node_srv = start_server(node_cfg).await.expect("start genuine node");
        let node_gw = format!("http://127.0.0.1:{}", node_srv.http_addr.port());

        let ca_router = crate::http::build_http_router(ca_srv.state.clone());

        // Helper to request a single-use challenge nonce from CA
        async fn fetch_challenge(
            router: &axum::Router,
            server_id: &str,
            gateway: &str,
        ) -> String {
            let uri = format!(
                "/api/directory/challenge?server_id={server_id}&gateway={}",
                gateway.replace("://", "%3A%2F%2F")
            );
            let resp = router
                .clone()
                .oneshot(axum::http::Request::get(uri).body(axum::body::Body::empty()).unwrap())
                .await
                .unwrap();
            assert_eq!(resp.status(), axum::http::StatusCode::OK);
            let bytes = http_body_util::BodyExt::collect(resp.into_body()).await.unwrap().to_bytes();
            let v: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
            v["nonce"].as_str().unwrap().to_string()
        }

        // 4. Genuine node obtains challenge, signs claim, submits heartbeat -> CA reverse probe succeeds -> 200 OK + verified=true
        let nonce1 = fetch_challenge(&ca_router, "community-genuine", &node_gw).await;
        let ts1 = crate::state::ServerState::now_unix_secs() as i64;
        let sig1 = crate::attestation::sign_attestation_claim(
            official_key,
            &nonce1,
            "community-genuine",
            &node_gw,
            &exe_hash,
            &assets_digest,
            "3.3.6",
            ts1,
        );
        let hb_genuine = serde_json::json!({
            "server_id": "community-genuine",
            "name": "Genuine Community Node",
            "gateway": node_gw,
            "region": "SEA",
            "online_players": 3,
            "max_players": 100,
            "password_required": false,
            "pvp_enabled": false,
            "version": "3.3.6",
            "nonce": nonce1,
            "ts": ts1,
            "exe_sha256": exe_hash,
            "assets_digest": assets_digest,
            "attestation_sig": sig1
        });
        let resp_ok = ca_router
            .clone()
            .oneshot(
                Request::post("/api/directory/heartbeat")
                    .header("content-type", "application/json")
                    .body(Body::from(serde_json::to_vec(&hb_genuine).unwrap()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp_ok.status(), axum::http::StatusCode::OK);
        let ok_bytes = http_body_util::BodyExt::collect(resp_ok.into_body()).await.unwrap().to_bytes();
        let ok_json: serde_json::Value = serde_json::from_slice(&ok_bytes).unwrap();
        assert_eq!(ok_json["verified"], true);

        // Replay same single-use nonce -> 403 Forbidden
        let resp_replay = ca_router
            .clone()
            .oneshot(
                Request::post("/api/directory/heartbeat")
                    .header("content-type", "application/json")
                    .body(Body::from(serde_json::to_vec(&hb_genuine).unwrap()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp_replay.status(), axum::http::StatusCode::FORBIDDEN);

        // 5. Tampered binary hash -> 403 Forbidden ("untrusted_server_binary")
        let nonce2 = fetch_challenge(&ca_router, "community-tampered", &node_gw).await;
        let ts2 = crate::state::ServerState::now_unix_secs() as i64;
        let bad_exe = "00000000000000000000000000000000000000000000000000000000deadbeef";
        let sig2 = crate::attestation::sign_attestation_claim(
            official_key,
            &nonce2,
            "community-tampered",
            &node_gw,
            bad_exe,
            &assets_digest,
            "3.3.6",
            ts2,
        );
        let hb_tampered = serde_json::json!({
            "server_id": "community-tampered",
            "name": "Tampered Server",
            "gateway": node_gw,
            "version": "3.3.6",
            "nonce": nonce2,
            "ts": ts2,
            "exe_sha256": bad_exe,
            "assets_digest": assets_digest,
            "attestation_sig": sig2
        });
        let resp_bad_exe = ca_router
            .clone()
            .oneshot(
                Request::post("/api/directory/heartbeat")
                    .header("content-type", "application/json")
                    .body(Body::from(serde_json::to_vec(&hb_tampered).unwrap()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp_bad_exe.status(), axum::http::StatusCode::FORBIDDEN);
        let bad_bytes = http_body_util::BodyExt::collect(resp_bad_exe.into_body()).await.unwrap().to_bytes();
        let bad_json: serde_json::Value = serde_json::from_slice(&bad_bytes).unwrap();
        assert_eq!(bad_json["error"], "untrusted_server_binary");

        // 6. Forged/wrong-key node failing active reverse HTTP probe (/api/attestation) -> 403 Forbidden
        let mut fake_cfg = ServerConfig::default();
        fake_cfg.http_bind = "127.0.0.1:0".into();
        fake_cfg.tcp_bind = "127.0.0.1:0".into();
        fake_cfg.server_name = "Forged Key Server".into();
        fake_cfg.db_path = Some(dir.join("fake.db"));
        fake_cfg.wildlife_wander_interval_ms = None;
        fake_cfg.attestation_key = Some("wrong-attacker-key".into());
        let fake_srv = start_server(fake_cfg).await.expect("start fake node");
        let fake_gw = format!("http://127.0.0.1:{}", fake_srv.http_addr.port());

        let nonce3 = fetch_challenge(&ca_router, "community-forged", &fake_gw).await;
        let ts3 = crate::state::ServerState::now_unix_secs() as i64;
        let sig3 = crate::attestation::sign_attestation_claim(
            official_key,
            &nonce3,
            "community-forged",
            &fake_gw,
            &exe_hash,
            &assets_digest,
            "3.3.6",
            ts3,
        );
        let hb_forged = serde_json::json!({
            "server_id": "community-forged",
            "name": "Forged Key Server",
            "gateway": fake_gw,
            "version": "3.3.6",
            "nonce": nonce3,
            "ts": ts3,
            "exe_sha256": exe_hash,
            "assets_digest": assets_digest,
            "attestation_sig": sig3
        });
        let resp_forged = ca_router
            .clone()
            .oneshot(
                Request::post("/api/directory/heartbeat")
                    .header("content-type", "application/json")
                    .body(Body::from(serde_json::to_vec(&hb_forged).unwrap()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp_forged.status(), axum::http::StatusCode::FORBIDDEN);

        // 7. CA /servers.json contains Genuine Community Node (verified: true) and neither Tampered nor Forged
        let resp_list = ca_router
            .clone()
            .oneshot(
                Request::get("/servers.json")
                    .header("host", format!("127.0.0.1:{}", ca_srv.http_addr.port()))
                    .body(Body::empty())
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp_list.status(), axum::http::StatusCode::OK);
        let list_bytes = http_body_util::BodyExt::collect(resp_list.into_body()).await.unwrap().to_bytes();
        let list_json: serde_json::Value = serde_json::from_slice(&list_bytes).unwrap();
        let servers = list_json["servers"].as_array().unwrap();
        let genuine_entry = servers
            .iter()
            .find(|s| s["name"] == "Genuine Community Node")
            .expect("genuine node must appear in CA /servers.json");
        assert_eq!(genuine_entry["verified"], true);
        assert!(servers.iter().all(|s| s["name"] != "Tampered Server"));
        assert!(servers.iter().all(|s| s["name"] != "Forged Key Server"));

        fake_srv.abort();
        node_srv.abort();
        ca_srv.abort();
        let _ = std::fs::remove_dir_all(&dir);
    }
}
