use crate::{queue::MusicQueue, track::*, ui::MessageUI};
use anyhow::Result;
use hourai::models::id::{ChannelId, UserId};
use std::collections::HashSet;
use twilight_lavalink::model::*;

pub struct PlayerState {
    pub skip_votes: HashSet<UserId>,
    pub queue: MusicQueue<UserId, Track>,
    pub now_playing_ui: Option<MessageUI>,
    pub queue_ui: Option<MessageUI>,
}

impl PlayerState {
    pub fn currently_playing(&self) -> Option<(UserId, Track)> {
        self.queue.peek().map(|item| (item.key, item.value.clone()))
    }

    pub fn is_playing(&self) -> bool {
        self.queue.peek().is_some()
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

    fn set_volume(&self, mut volume: u32) -> Result<()> {
        if volume > 150 {
            volume = 150;
        }
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
