use anyhow::Result;
use crate::{queue::MusicQueue, track::*};
use hourai::models::id::{UserId, ChannelId};
use std::collections::HashSet;
use twilight_lavalink::model::*;

pub struct PlayerState {
    pub skip_votes: HashSet<UserId>,
    pub queue: MusicQueue<UserId, Track>,
}

impl PlayerState {
    pub fn currently_playing(&self) -> Option<(UserId, Track)> {
        self.queue.peek().map(|item| (item.key, item.value.clone()))
    }
}

pub trait PlayerExt {
    fn as_player(&self) -> &twilight_lavalink::player::Player;

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
