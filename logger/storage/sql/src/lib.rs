mod models;
mod types;
pub mod whois;

pub use self::models::*;
pub use sqlx::{self, types as sql_types, Error, Executor, Result};
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
