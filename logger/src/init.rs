use crate::config::HouraiConfig;
use crate::db::RedisPool;
use tracing::debug;

pub fn init(config: &HouraiConfig) {
    tracing_subscriber::fmt()
        .with_level(true)
        .with_thread_ids(true)
        .with_timer(tracing_subscriber::fmt::time::ChronoUtc::rfc3339())
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .compact()
        .init();

    debug!("Loaded Config: {:?}", config);
}

pub fn http_client(config: &HouraiConfig) -> twilight_http::Client {
    debug!("Creating Discord HTTP client");
    // Use the twilight HTTP proxy when configured
    if let Some(proxy) = config.discord.proxy.as_ref() {
        twilight_http::Client::builder()
            .token(&config.discord.bot_token)
            .proxy(proxy, true)
            .ratelimiter(None)
            .build()
    } else {
        twilight_http::Client::new(&config.discord.bot_token)
    }
}

pub async fn sql(config: &HouraiConfig) -> sqlx::PgPool {
    debug!("Creating Postgres client");
    sqlx::postgres::PgPoolOptions::new()
         .max_connections(3)
         .connect(&config.database)
         .await
         .expect("Failed to initialize SQL connection pool")
}

pub async fn redis(config: &HouraiConfig) -> RedisPool {
    debug!("Creating Redis client");
    let client = redis::Client::open(config.redis.as_ref())
                               .expect("Failed to create Redis client");
    redis::aio::ConnectionManager::new(client)
          .await
          .expect("Failed to initialize multiplexed Redis connection")
}
