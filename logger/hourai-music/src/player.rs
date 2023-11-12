use crate::{interaction_ui, queue::MusicQueue, track::*};
use anyhow::Result;
use hourai::{
    models::id::{marker::*, Id},
    proto::music_bot::*,
};
use std::collections::HashSet;
use twilight_lavalink::model::*;

#[derive(Default)]
pub struct PlayerState {
    pub skip_votes: HashSet<Id<UserMarker>>,
    pub queue: MusicQueue<Id<UserMarker>, Track>,
    pub now_playing_ui: Option<interaction_ui::MessageUI>,
    pub queue_ui: Option<interaction_ui::MessageUI>,

    pub queue_page: i64,
}

impl PlayerState {
    pub fn currently_playing(&self) -> Option<(Id<UserMarker>, Track)> {
        self.queue.peek().map(|item| (item.key, item.value.clone()))
    }

    pub fn is_playing(&self) -> bool {
        self.queue.peek().is_some()
    }

    pub fn save_to_proto(&self) -> MusicStateProto {
        let mut proto = MusicStateProto::new();
        *proto.mut_queue() = MusicQueueProto::from(&self.queue);
        proto
            .skip_votes
            .extend(self.skip_votes.iter().map(|id| id.get()));
        proto
    }

    pub fn load_from_proto(&mut self, mut state: MusicStateProto) {
        self.queue = state.take_queue().into();
        self.skip_votes = state.skip_votes.into_iter().map(Id::new).collect();
    }
}

pub trait PlayerExt {
    fn as_player(&self) -> &twilight_lavalink::player::Player;

    fn play(&self, track: &Track) -> Result<()> {
        let player = self.as_player();
        player.send(track.play(player.guild_id()))?;
        Ok(())
    }

    fn seek(&self, position: i64) -> Result<()> {
        let player = self.as_player();
        player.send(Seek::new(player.guild_id(), position))?;
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

impl From<MusicQueueProto> for MusicQueue<Id<UserMarker>, Track> {
    fn from(value: MusicQueueProto) -> Self {
        let mut queue = Self::default();
        for user_queue in value.user_queues {
            let user_id = Id::new(user_queue.get_user_id());
            for track in user_queue.tracks {
                queue.push(user_id, track.into());
            }
        }
        queue
    }
}

impl From<&MusicQueue<Id<UserMarker>, Track>> for MusicQueueProto {
    fn from(value: &MusicQueue<Id<UserMarker>, Track>) -> Self {
        let mut proto = Self::new();
        for key in value.keys() {
            let mut queue = UserQueueProto::new();
            queue.set_user_id(key.get());
            for track in value.iter_with_key(key) {
                queue.tracks.push(track.clone().into());
            }
            proto.user_queues.push(queue);
        }
        proto
    }
}
