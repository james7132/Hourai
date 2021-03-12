use anyhow::Result;
use hourai::prelude::*;
use crate::{queue::MusicQueue, track::*};
use std::collections::HashSet;
use hourai::gateway::Cluster;
use twilight_lavalink::model::*;
use hourai::models::id::{UserId, ChannelId};

pub struct PlayerState {
    pub channel_id: ChannelId,
    pub skip_votes: HashSet<UserId>,
    pub queue: MusicQueue<UserId, Track>,
}

impl PlayerState {
    pub fn currently_playing(&self) -> Option<(UserId, &Track)> {
        self.queue.peek().map(|item| (item.key, item.value))
    }
}

pub trait PlayerExt {
    fn as_player(&self) -> &twilight_lavalink::player::Player;

    fn connect(&self, gateway: Cluster, channel_id: ChannelId) {
        let player = self.as_player();
        let guild_id = player.guild_id();
        tokio::spawn(async move {
            let shard_id = gateway.shard_id(guild_id);
            let result = gateway.command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": channel_id,
                    "guild_id": guild_id,
                    "self_mute": false,
                    "self_deaf": false,
                }
            })).await;

            if let Err(err) = result {
                error!("Error while connecting to channel {} in guild {}: {}",
                        channel_id, guild_id, err);
            } else {
                info!("Connected to channel {} in guild {}", channel_id, guild_id);
            }
        });
    }

    fn disconnect(&self, gateway: Cluster) -> Result<()> {
        let player = self.as_player();
        let guild_id = player.guild_id();
        player.send(Stop::new(guild_id))?;
        info!("Stopped playing in in guild {}", guild_id);
        tokio::spawn(async move {
            let shard_id = gateway.shard_id(guild_id);
            let result = gateway.command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": None::<ChannelId>,
                    "guild_id": guild_id,
                    "self_mute": false,
                    "self_deaf": false,
                }
            })).await;

            if let Err(err) = result {
                error!("Error while disconnecting from guild {}: {}", guild_id, err);
            } else {
                info!("Disconnected from guild {}", guild_id);
            }
        });
        Ok(())
    }

    fn play(&self, track: &Track) -> Result<()> {
        let player = self.as_player();
        player.send(track.play(player.guild_id()))?;
        Ok(())
    }

    fn set_pause(&self, paused: bool) -> Result<()> {
        let player = self.as_player();
        player.send(Pause::from((player.guild_id(), paused)))?;
        Ok(())
    }

    fn set_volume(&self, volume: u8) -> Result<()> {
        let player = self.as_player();
        player.send(Volume::from((player.guild_id(), volume as i64)))?;
        Ok(())
    }
}

impl PlayerExt for twilight_lavalink::player::Player {
    fn as_player(&self) -> &twilight_lavalink::player::Player {
        self
    }
}
