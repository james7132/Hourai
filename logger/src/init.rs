use crate::config::HouraiConfig;
use crate::db::RedisPool;
use mobc_redis::{redis, RedisConnectionManager};

pub fn create_http_client(config: &HouraiConfig) -> twilight_http::Client {
    // Use the twilight HTTP proxy when configured
    if let Some(proxy) = config.discord.proxy.clone() {
        twilight_http::Client::builder()
            .token(&config.discord.bot_token)
            .proxy(proxy, true)
            .ratelimiter(None)
            .build()
    } else {
        twilight_http::Client::new(&config.discord.bot_token)
    }
}

pub async fn create_pg_pool(config: &HouraiConfig) -> sqlx::PgPool {
    sqlx::postgres::PgPoolOptions::new()
        .max_connections(10)
        .connect(&config.database)
        .await
        .expect("Failed to initialize SQL connection pool")
}

pub fn create_redis_pool(config: &HouraiConfig) -> RedisPool {
    let client = redis::Client::open(config.redis.as_ref())
                               .expect("Failed to create Redis client");
    let manager = RedisConnectionManager::new(client);
    mobc::Pool::builder().max_open(10).build(manager)
}
