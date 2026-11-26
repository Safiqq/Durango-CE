use std::path::PathBuf;

use durango_server::start_server;
use durango_server::state::ServerConfig;

fn discover_dir(env_var: &str, candidates: &[&str]) -> Option<PathBuf> {
    if let Ok(v) = std::env::var(env_var) {
        let p = PathBuf::from(v);
        if p.is_dir() {
            return Some(p);
        }
    }
    for c in candidates {
        let p = PathBuf::from(c);
        if p.is_dir() {
            return Some(p);
        }
    }
    None
}

fn parse_rust_log_level() -> tracing::Level {
    let raw = std::env::var("RUST_LOG").unwrap_or_default();
    for token in raw.split(',').rev() {
        let level_str = token.rsplit('=').next().unwrap_or(token).trim();
        if level_str.eq_ignore_ascii_case("trace") {
            return tracing::Level::TRACE;
        } else if level_str.eq_ignore_ascii_case("debug") {
            return tracing::Level::DEBUG;
        } else if level_str.eq_ignore_ascii_case("info") {
            return tracing::Level::INFO;
        } else if level_str.eq_ignore_ascii_case("warn") || level_str.eq_ignore_ascii_case("warning") {
            return tracing::Level::WARN;
        } else if level_str.eq_ignore_ascii_case("error") || level_str.eq_ignore_ascii_case("off") {
            return tracing::Level::ERROR;
        }
    }
    tracing::Level::INFO
}

async fn wait_for_shutdown_signal() {
    #[cfg(unix)]
    {
        use tokio::signal::unix::{signal, SignalKind};
        let mut sigterm = signal(SignalKind::terminate()).ok();
        tokio::select! {
            _ = tokio::signal::ctrl_c() => {
                tracing::info!("received SIGINT; flushing live player state...");
            }
            _ = async {
                if let Some(ref mut s) = sigterm {
                    s.recv().await;
                } else {
                    std::future::pending::<()>().await;
                }
            } => {
                tracing::info!("received SIGTERM; flushing live player state...");
            }
        }
    }
    #[cfg(not(unix))]
    {
        let _ = tokio::signal::ctrl_c().await;
        tracing::info!("received Ctrl-C; flushing live player state...");
    }
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    tracing_subscriber::fmt()
        .with_max_level(parse_rust_log_level())
        .init();

    let mut cfg = ServerConfig::default();
    if let Ok(v) = std::env::var("DURANGO_HTTP_ADDR") {
        cfg.http_bind = v;
    }
    if let Ok(v) = std::env::var("DURANGO_TCP_ADDR") {
        cfg.tcp_bind = v;
    }
    if let Ok(v) = std::env::var("DURANGO_PUBLIC_HOST") {
        cfg.public_host = v;
    }
    if let Ok(v) = std::env::var("DURANGO_SERVER_NAME") {
        if !v.trim().is_empty() {
            cfg.server_name = v.trim().to_string();
        }
    }
    if let Ok(v) = std::env::var("DURANGO_SERVER_MODE") {
        if !v.trim().is_empty() {
            cfg.server_mode = v.trim().to_string();
        }
    }
    if let Ok(v) = std::env::var("DURANGO_REGION") {
        if !v.trim().is_empty() {
            cfg.region_tag = v.trim().to_string();
        }
    }
    if let Ok(v) = std::env::var("DURANGO_MAX_PLAYERS") {
        if let Ok(n) = v.trim().parse::<usize>() {
            cfg.max_players = n.clamp(1, 5000);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_DIRECTORY_URL") {
        let trimmed = v.trim().trim_end_matches('/').to_string();
        if !trimmed.is_empty() {
            cfg.directory_url = Some(trimmed);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_DIRECTORY_SECRET") {
        let trimmed = v.trim().to_string();
        if !trimmed.is_empty() {
            cfg.directory_secret = Some(trimmed);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_UPSTREAM_SERVERS_URLS") {
        cfg.upstream_servers_urls = v
            .split(|c: char| c == ',' || c.is_whitespace())
            .filter_map(durango_server::normalize_servers_json_url)
            .collect();
    }
    if let Ok(v) = std::env::var("DURANGO_ATTESTATION_KEY") {
        let trimmed = v.trim().to_string();
        if !trimmed.is_empty() {
            cfg.attestation_key = Some(trimmed);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_CA_ALLOWLIST_FILE") {
        let trimmed = v.trim();
        if !trimmed.is_empty() {
            cfg.ca_allowlist_path = Some(PathBuf::from(trimmed));
        }
    }
    if let Ok(v) = std::env::var("DURANGO_CA_REQUIRE_ATTESTATION") {
        let t = v.trim().to_ascii_lowercase();
        cfg.ca_require_attestation = matches!(t.as_str(), "1" | "true" | "yes");
    }
    if let Ok(v) = std::env::var("DURANGO_JOIN_PASSWORD") {
        let trimmed = v.trim().to_string();
        if !trimmed.is_empty() {
            cfg.join_password = Some(trimmed);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_ADMIN_TOKEN") {
        let trimmed = v.trim().to_string();
        if !trimmed.is_empty() {
            cfg.admin_token = Some(trimmed);
        }
    }
    if let Ok(v) = std::env::var("DURANGO_ADMIN_KEYS") {
        cfg.admin_keys = v
            .split(',')
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(ToString::to_string)
            .collect();
    }
    if let Ok(v) = std::env::var("DURANGO_NOTICE") {
        cfg.notice_text = v;
    }
    cfg.assets_dir = discover_dir(
        "DURANGO_ASSETS_DIR",
        &[
            ".work/data/assets",
            "_backup_2026-11-19/workspace-artifacts/build/server/data/assets",
        ],
    );
    cfg.terrains_dir = discover_dir(
        "DURANGO_TERRAINS_DIR",
        &[
            ".work/data/terrains",
            "_backup_2026-11-19/workspace-artifacts/build/server/data/terrains",
        ],
    );
    cfg.db_path = Some(
        std::env::var("DURANGO_DB_PATH")
            .map(PathBuf::from)
            .unwrap_or_else(|_| PathBuf::from(".work/durango.db")),
    );

    let running = start_server(cfg.clone()).await?;
    tracing::info!(
        http = %running.http_addr,
        tcp = %running.tcp_addr,
        assets = ?cfg.assets_dir,
        terrains = ?cfg.terrains_dir,
        db = ?cfg.db_path,
        "Durango-CE Rust Server ready (HTTP Gateway + TCP Game Server + SQLite)"
    );

    wait_for_shutdown_signal().await;
    let flushed = running.state.flush_all_live_clients().await;
    tracing::info!(flushed, "flushed connected players; shutting down Durango-CE Rust Server...");
    running.abort();
    Ok(())
}
