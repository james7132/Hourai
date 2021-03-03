use crate::config::HouraiConfig;
use crate::db::RedisPool;
use std::sync::{Arc, Mutex};
use mobc_redis::{redis, RedisConnectionManager};

/// A common initialization struct for ensuring multiple connections to remote resources are
struct InitializerRef {
    config: HouraiConfig,
    http_client: Option<twilight_http::Client>,
    sql: Option<sqlx::PgPool>,
    redis: Option<RedisPool>,
}

#[derive(Clone)]
pub struct Initializer(Arc<Mutex<InitializerRef>>);

impl Initializer {

    pub fn new(config: HouraiConfig) -> Self {
        Self(Arc::new(Mutex::new(InitializerRef {
            config: config,
            http_client: None,
            sql: None,
            redis: None,
        })))
    }

    pub fn config(&self) -> HouraiConfig {
        let init = self.0.lock().unwrap();
        (*init).config.clone()
    }

    pub fn http_client(&self) -> twilight_http::Client {
        let mut init = self.0.lock().unwrap();
        let config = &init.config;

        if let Some(client) = init.http_client.clone() {
            return client;
        }

        // Use the twilight HTTP proxy when configured
        let client = if let Some(proxy) = config.discord.proxy.as_ref() {
            twilight_http::Client::builder()
                .token(&config.discord.bot_token)
                .proxy(proxy, true)
                .ratelimiter(None)
                .build()
        } else {
            twilight_http::Client::new(&config.discord.bot_token)
        };

        init.http_client.replace(client.clone());
        client
    }

    pub async fn sql(&self) -> sqlx::PgPool {
        let mut init = self.0.lock().unwrap();

        if let Some(sql) = init.sql.clone() {
            return sql;
        }

        let sql = sqlx::postgres::PgPoolOptions::new()
                        .max_connections(3)
                        .connect(&init.config.database)
                        .await
                        .expect("Failed to initialize SQL connection pool");

        init.sql.replace(sql.clone());
        sql
    }

    pub fn redis(&self) -> RedisPool {
        let mut init = self.0.lock().unwrap();

        if let Some(redis) = init.redis.clone() {
            return redis;
        }

        let client = redis::Client::open(self.config().redis.as_ref())
                                   .expect("Failed to create Redis client");
        let manager = RedisConnectionManager::new(client);
        let redis = mobc::Pool::builder().max_open(10).build(manager);

        init.redis.replace(redis.clone());
        redis
    }

}

