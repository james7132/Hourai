use thiserror::Error as ErrorTrait;

/// The sum type of all errors that might result from fetching
#[derive(ErrorTrait, Debug)]
pub enum Error {
    #[error("SQL error {:?}", .0)]
    Sql(#[from] sqlx::Error),
    #[error("Redis error: {:?}", .0)]
    Redis(#[from] mobc_redis::redis::RedisError),
    #[error("Protobuf error: {:?}", .0)]
    Protobuf(#[from] protobuf::ProtobufError),
    #[error("IO error: {:?}", .0)]
    Io(#[from] std::io::Error),
    #[error("JSON error: {:?}", .0)]
    Json(#[from] serde_json::Error),
    #[error("Gateway error: {:?}", .0)]
    ClusterError(#[from] twilight_gateway::cluster::ClusterSendError)
}

pub type Result<T> = std::result::Result<T, Error>;
