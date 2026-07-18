mod guild_config;
mod oauth;
pub mod prelude;
mod status;

use actix_web::{dev::Server, web, App, HttpServer};
use anyhow::Result;
use hourai::config::HouraiConfig;
use hourai_redis::RedisClient;
use hourai_sql::SqlPool;

pub(crate) struct AppState {
    pub config: HouraiConfig,
    pub http: awc::Client,
    pub sql: SqlPool,
    pub redis: RedisClient,
}

pub fn api(cfg: &mut web::ServiceConfig) {
    cfg.service(
        web::scope("/v1")
            .service(web::scope("/bot").configure(status::scoped_config))
            .service(web::scope("/guilds").configure(guild_config::scoped_config)),
    );
    // OAuth is not versioned
    cfg.service(web::scope("/oauth").configure(oauth::scoped_config));
}

pub fn run_server(config: HouraiConfig, sql: SqlPool, redis: RedisClient) -> Result<Server> {
    let port = config.web.port;
    tracing::info!("Starting Actix-web server on port {}", port);

    let server = HttpServer::new(move || {
        App::new()
            .wrap(tracing_actix_web::TracingLogger::default())
            .app_data(web::Data::new(AppState {
                config: config.clone(),
                http: awc::Client::new(),
                sql: sql.clone(),
                redis: redis.clone(),
            }))
            .service(web::scope("/api").configure(api))
    })
    .bind(format!("0.0.0.0:{}", port))?
    .run();

    Ok(server)
}
