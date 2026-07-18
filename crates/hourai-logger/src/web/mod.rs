mod guild_config;
mod oauth;
pub mod prelude;
mod status;

use anyhow::Result;
use axum::{routing::get, Router};
use hourai::config::HouraiConfig;
use hourai_redis::RedisClient;
use hourai_sql::SqlPool;
use std::sync::Arc;
use tower_cookies::CookieManagerLayer;
use tower_http::trace::TraceLayer;

pub(crate) struct AppState {
    pub config: HouraiConfig,
    pub http: reqwest::Client,
    pub sql: SqlPool,
    pub redis: RedisClient,
}

pub fn app(state: Arc<AppState>) -> Router {
    let api = Router::new()
        .nest(
            "/v1",
            Router::new()
                .route("/bot/status", get(status::bot_status))
                .nest("/guilds", guild_config::router()),
        )
        .nest("/oauth", oauth::router())
        .with_state(state);

    Router::new()
        .nest("/api", api)
        .layer(CookieManagerLayer::new())
        .layer(TraceLayer::new_for_http())
}

pub async fn run_server(config: HouraiConfig, sql: SqlPool, redis: RedisClient) -> Result<()> {
    let port = config.web.port;
    tracing::info!("Starting Axum web server on port {}", port);

    let state = Arc::new(AppState {
        config,
        http: reqwest::Client::new(),
        sql,
        redis,
    });

    let router = app(state);
    let listener = tokio::net::TcpListener::bind(format!("0.0.0.0:{}", port)).await?;
    axum::serve(listener, router).await?;

    Ok(())
}
