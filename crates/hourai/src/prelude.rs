use crate::models::id::{marker::GuildMarker, Id};
pub use std::{sync::Arc, time::Duration};
pub use tracing::{debug, error, info, warn};

pub trait ClusterExt {
    fn total_shards(&self) -> usize;

    /// Gets the shard ID for a guild.
    #[inline(always)]
    fn shard_id(&self, guild_id: Id<GuildMarker>) -> u64 {
        let total = self.total_shards();
        if total == 0 {
            0
        } else {
            (guild_id.get() >> 22) % (total as u64)
        }
    }
}

impl ClusterExt for [twilight_gateway::Shard] {
    #[inline(always)]
    fn total_shards(&self) -> usize {
        self.len()
    }
}

impl ClusterExt for Vec<twilight_gateway::Shard> {
    #[inline(always)]
    fn total_shards(&self) -> usize {
        self.len()
    }
}

impl<T: ClusterExt> ClusterExt for Arc<T> {
    #[inline(always)]
    fn total_shards(&self) -> usize {
        (**self).total_shards()
    }
}
