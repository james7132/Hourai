pub use std::{time::Duration, sync::Arc};
pub use crate::error::*;
pub use twilight_model::id::{GuildId, UserId};
pub use crate::db::RedisPool;
pub use futures::stream::StreamExt;
pub use tracing::{info, warn, debug, error};
pub use async_trait::async_trait;

pub trait ClusterExt {
    fn total_shards(&self) -> u64;

    /// Gets the shard ID for a guild.
    #[inline(always)]
    fn shard_id(&self, guild_id: GuildId) -> u64 {
        (guild_id.0 >> 22) % self.total_shards()
    }
}

pub trait UserExt {
    fn discriminator(&self) -> u16;
}

impl ClusterExt for twilight_gateway::cluster::Cluster {
    #[inline(always)]
    fn total_shards(&self) -> u64 {
        let shards = self.config().shard_config().shard()[1];
        assert!(shards > 0, "Bot somehow has a total of zero shards.");
        shards
    }
}

impl UserExt for twilight_model::user::User {
    fn discriminator(&self) -> u16 {
        self.discriminator
            .parse::<u16>()
            .expect("The user's discriminator must be parseable as an integer")
    }
}
