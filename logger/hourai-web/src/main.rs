#[macro_use]
extern crate lazy_static;

mod oauth;
mod guild_config;

use hourai::{init, config};
use serde::Serialize;
use actix_web::{web, get, App, HttpServer, Responder};

#[derive(Serialize)]
struct BotStatus {
    shards: Vec<ShardStatus>
}

#[derive(Serialize)]
struct ShardStatus {
    shard_id: u16,
    guilds: i64,
    members: i64
}

#[get("/api/v1/bot/status")]
async fn bot_status(data: web::Data<AppState>) -> Result<web::Json<BotStatus>, sqlx::Error> {
    Ok(web::Json(BotStatus {
        shards: vec![ShardStatus {
            shard_id: 0,
            guilds: hourai_sql::Member::count_guilds().fetch_one(&data.sql).await?.0,
            members: hourai_sql::Member::count_members().fetch_one(&data.sql).await?.0,
        }]
    })
}

pub(crate) struct AppState {
    config: hourai::config::HouraiConfig,
    hyper: hyper::Client<hyper::client::HttpConnector, hyper::Body>,
    sql: hourai_sql::SqlPool,
    redis: hourai_redis::RedisPool
}

#[actix_web::main]
async fn main() -> std::io::Result<()> {
    let config = config::load_config(config::get_config_path().as_ref());
    init::init(&config);

    let sql = hourai_sql::init(&config).await;
    let redis = hourai_redis::init(&config).await;

    HttpServer::new(move || {
        App::new()
            .data(AppState {
                config,
                hyper: hyper::Client::new(),
                sql: sql.clone(),
                redis: redis.clone()
            })
            .service(bot_status)
    })
    .bind(format!("127.0.0.1:{}", config.web.port))?
    .run()
    .await
}
