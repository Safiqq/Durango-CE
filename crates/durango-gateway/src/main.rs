use std::path::PathBuf;

use durango_gateway::{build_router, GatewayConfig, GatewayState};

fn discover_assets_dir() -> Option<PathBuf> {
    if let Ok(env_dir) = std::env::var("DURANGO_ASSETS_DIR") {
        let p = PathBuf::from(env_dir);
        if p.is_dir() {
            return Some(p);
        }
    }
    let candidates = [
        ".work/data/assets",
        "_backup_2026-11-19/workspace-artifacts/build/server/data/assets",
    ];
    for c in candidates {
        let p = PathBuf::from(c);
        if p.is_dir() {
            return Some(p);
        }
    }
    None
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::INFO)
        .init();

    let mut config = GatewayConfig::default();
    if let Ok(bind) = std::env::var("DURANGO_BIND_ADDR") {
        config.bind_addr = bind;
    }
    if let Ok(port) = std::env::var("DURANGO_GAME_PORT") {
        if let Ok(p) = port.parse() {
            config.game_tcp_port = p;
        }
    }
    config.assets_dir = discover_assets_dir();

    let listener = tokio::net::TcpListener::bind(&config.bind_addr).await?;
    tracing::info!(
        bind = %config.bind_addr,
        game_port = config.game_tcp_port,
        assets_dir = ?config.assets_dir,
        "Durango-CE Rust Gateway ready"
    );

    let app = build_router(GatewayState::new(config));
    axum::serve(listener, app).await?;
    Ok(())
}
