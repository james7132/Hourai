mod models;
mod types;
pub mod whois;

pub use self::models::*;
pub use sqlx::types as sql_types;
pub use sqlx::*;
use tracing::debug;

pub type SqlPool = sqlx::Pool<SqlDatabase>;

pub async fn migrate(pool: &sqlx::PgPool) -> std::result::Result<(), sqlx::migrate::MigrateError> {
    sqlx::migrate!("./migrations").run(pool).await
}

pub async fn init(config: &hourai::config::HouraiConfig) -> sqlx::PgPool {
    debug!("Creating Postgres client");
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(3)
        .connect(&config.database)
        .await
        .expect("Failed to initialize SQL connection pool");

    migrate(&pool).await.expect("Failed to run SQL migrations");

    pool
}
