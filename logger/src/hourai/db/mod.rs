mod models;
mod cache;

// Include the auto-generated protos as a module
mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

pub use self::models::*;
pub use self::cache::*;
use crate::error::Result;
use crate::config::HouraiConfig;
use mobc_redis::{redis, RedisConnectionManager};

pub type RedisPool = mobc::Pool<mobc_redis::RedisConnectionManager>;

pub async fn create_pg_pool(config: &HouraiConfig) -> Result<sqlx::PgPool> {
    Ok(sqlx::postgres::PgPoolOptions::new()
        .max_connections(10)
        .connect(&config.database)
        .await?)
}

pub fn create_redis_pool(config: &HouraiConfig) -> Result<RedisPool> {
    let client = redis::Client::open(config.redis.as_ref())?;
    let manager = RedisConnectionManager::new(client);
    return Ok(mobc::Pool::builder().max_open(10).build(manager));
}
