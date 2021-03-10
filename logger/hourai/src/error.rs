pub use anyhow::Result;
pub use crate::commands::CommandError;
use twilight_model::id::*;
use thiserror::Error as ErrorTrait;

/// The sum type of all errors that might result from fetching
#[derive(ErrorTrait, Debug)]
pub enum Error {
    #[error("SQL error {:?}", .0)]
    Sql(#[from] sqlx::Error),
    #[error("Redis error: {:?}", .0)]
    Redis(#[from] redis::RedisError),
    #[error("Protobuf error: {:?}", .0)]
    Protobuf(#[from] protobuf::ProtobufError),
    #[error("IO error: {:?}", .0)]
    Io(#[from] std::io::Error),
    #[error("JSON error: {:?}", .0)]
    Json(#[from] serde_json::Error),
    #[error("Discord Gateway error: {:?}", .0)]
    DiscordGatewayError(#[from] twilight_gateway::cluster::ClusterCommandError),
    #[error("HTTP error: {:?}", .0)]
    HttpError(#[from] twilight_http::Error),
    #[error("Cache error: {:?}", .0)]
    CacheError(#[from] CacheNotFound),
    #[error("Command error: {:?}", .0)]
    CommandError(#[from] CommandError),
}

#[derive(ErrorTrait, Debug)]
pub enum CacheNotFound {
    #[error("Missing Guild: {}", .0)]
    Guild(GuildId),
    #[error("Missing GuildChannel: {}", .0)]
    GuildChannel(ChannelId),
    #[error("Missing role: {}", .0)]
    Role(RoleId),
    #[error("Missing user: {}", .0)]
    User(UserId),
    #[error("Missing member: {} (guild: {})", .0, .1)]
    Member(UserId, GuildId),
}
