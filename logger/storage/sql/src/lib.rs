mod models;
mod types;

pub use self::models::*;
pub use sqlx::types as sql_types;
pub use sqlx::*;
use tracing::debug;

pub type SqlPool = sqlx::Pool<SqlDatabase>;

pub async fn init(config: &hourai::config::HouraiConfig) -> sqlx::PgPool {
    debug!("Creating Postgres client");
    sqlx::postgres::PgPoolOptions::new()
        .max_connections(3)
        .connect(&config.database)
        .await
        .expect("Failed to initialize SQL connection pool")
}
