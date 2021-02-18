use crate::config::HouraiConfig;
use mobc_redis::{redis, RedisConnectionManager};

mod models;
mod cache;

// Include the auto-generated protos as a module
mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

pub use self::models::*;

pub struct Storage {
}

/// The sum type of all errors that might result from fetching
#[derive(Debug)]
pub enum StorageError {
    Sql(sqlx::Error),
    Redis(redis::RedisError),
    Protobuf(protobuf::ProtobufError),
    Io(std::io::Error),
}

impl From<sqlx::Error> for StorageError {
    fn from(err: sqlx::Error) -> StorageError {
        return StorageError::Sql(err);
    }
}

impl From<redis::RedisError> for StorageError {
    fn from(err: redis::RedisError) -> StorageError {
        return StorageError::Redis(err);
    }
}

impl From<protobuf::ProtobufError> for StorageError {
    fn from(err: protobuf::ProtobufError) -> StorageError {
        return StorageError::Protobuf(err);
    }
}

impl From<std::io::Error> for StorageError {
    fn from(err: std::io::Error) -> StorageError {
        return StorageError::Io(err);
    }
}

pub type Result<T> = std::result::Result<T, StorageError>;

pub type RedisPool = mobc::Pool<mobc_redis::RedisConnectionManager>;

pub async fn create_pg_pool(config: &HouraiConfig) -> sqlx::Result<sqlx::PgPool> {
    return Ok(sqlx::postgres::PgPoolOptions::new()
        .max_connections(10)
        .connect(&config.database)
        .await?);
}

pub fn create_redis_pool(config: &HouraiConfig) -> redis::RedisResult<RedisPool> {
    let client = redis::Client::open(config.redis.as_ref())?;
    let manager = RedisConnectionManager::new(client);
    return Ok(mobc::Pool::builder().max_open(10).build(manager));
}
