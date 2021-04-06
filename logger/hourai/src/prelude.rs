use crate::models::id::GuildId;
pub use std::{sync::Arc, time::Duration};
pub use tracing::{debug, error, info, warn};

pub trait ClusterExt {
    fn total_shards(&self) -> u64;

    /// Gets the shard ID for a guild.
    #[inline(always)]
    fn shard_id(&self, guild_id: GuildId) -> u64 {
        (guild_id.0 >> 22) % self.total_shards()
    }
}

impl ClusterExt for twilight_gateway::cluster::Cluster {
    #[inline(always)]
    fn total_shards(&self) -> u64 {
        let shards = self.config().shard_config().shard()[1];
        assert!(shards > 0, "Bot somehow has a total of zero shards.");
        shards
    }
}
