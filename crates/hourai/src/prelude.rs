use crate::models::id::{marker::GuildMarker, Id};
pub use std::{sync::Arc, time::Duration};
pub use tracing::{debug, error, info, warn};

pub trait ClusterExt {
    fn total_shards(&self) -> usize;

    /// Gets the shard ID for a guild.
    #[inline(always)]
    fn shard_id(&self, guild_id: Id<GuildMarker>) -> u64 {
        (guild_id.get() >> 22) % (self.total_shards() as u64)
    }
}

impl ClusterExt for twilight_gateway::cluster::Cluster {
    #[inline(always)]
    fn total_shards(&self) -> usize {
        self.shards().len()
    }
}
