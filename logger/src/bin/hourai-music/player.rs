use hourai::prelude::*;
use crate::{Client, queue::MusicQueue, track::*};
use std::collections::HashSet;
use dashmap::DashMap;
use std::sync::{Weak, RwLock, RwLockReadGuard, RwLockWriteGuard};
use twilight_gateway::Cluster;
use twilight_lavalink::{player::PlayerManager as LavalinkPlayerManager, model::*};
use twilight_model::id::{UserId, GuildId, ChannelId};

macro_rules! get_lavalink_player {
    ($self:expr) => {
        $self.0
             .lavalink_manager
             .get(&$self.0.guild_id)
             .expect("twilight-lavalink player should have been created.")
             .value()
    };
}

pub struct PlayerManager(DashMap<GuildId, Player>);

impl PlayerManager {

    pub fn new() -> Self {
        Self(DashMap::new())
    }

    pub fn get_player(&self, guild_id: GuildId) -> Option<Player> {
        self.0.get(&guild_id).map(|kv| kv.value().clone())
    }

    pub fn add_player(&self, player: &Player) {
        debug!("Created player for guild {}", player.guild_id());
        self.0.insert(player.guild_id(), player.clone());
    }

    pub fn destroy_player(&self, guild_id: GuildId) {
        debug!("Destroyed player for guild {}", guild_id);
        self.0.remove(&guild_id);
    }

}

struct PlayerState {
    channel_id: Option<ChannelId>,
    currently_playing: Option<(UserId, TrackInfo)>,
    skip_votes: HashSet<UserId>,
    queue: MusicQueue<UserId, Track>,
}

struct PlayerRef {
    state: RwLock<PlayerState>,
    manager: Weak<PlayerManager>,
    lavalink_manager: LavalinkPlayerManager,
    gateway: Cluster,
    guild_id: GuildId,
    volume: u8,
}

impl Drop for PlayerRef {

    fn drop(&mut self) {
        debug!("Dropped player for guild {}", self.guild_id);
    }

}

#[derive(Clone)]
pub struct Player(Arc<PlayerRef>);

impl Player {

    pub async fn new<'a>(client: &Client<'a>, guild_id: GuildId) -> Result<Self> {
        // Ensure that there exists a player managed by twilight-lavalink
        client.lavalink.player(guild_id).await?;

        let player = Self(Arc::new(PlayerRef {
            manager: Arc::downgrade(&client.players),
            lavalink_manager: client.lavalink.players().clone(),
            gateway: client.gateway.clone(),
            guild_id: guild_id,
            volume: 100,
            state: RwLock::new(PlayerState {
                channel_id: None,
                currently_playing: None,
                skip_votes: HashSet::new(),
                queue: MusicQueue::new()
            }),
        }));

        client.players.add_player(&player);

        Ok(player)
    }

    fn state(&self) -> RwLockReadGuard<PlayerState> {
        self.0.state.read().expect("Player state lock has been poisoned")
    }

    fn state_mut(&self) -> RwLockWriteGuard<PlayerState> {
        self.0.state.write().expect("Player state lock has been poisoned")
    }

    pub fn guild_id(&self) -> GuildId {
        self.0.guild_id
    }

    pub fn channel_id(&self) -> Option<ChannelId> {
        self.state().channel_id.clone()
    }

    pub fn volume(&self) -> u8 {
        self.0.volume
    }

    /// Queues up a track to be played.
    pub fn enqueue(&self, user_id: UserId, tracks: impl IntoIterator<Item=Track>) {
        self.state_mut().queue.extend(user_id, tracks);
    }

    /// Removes all of a user's tracks from the queue.
    pub fn clear_user(&self, user_id: UserId) -> Option<usize> {
        self.state_mut().queue.clear_key(user_id)
    }

    /// Shuffle's all of a user's tracks in the queue.
    pub fn shuffle(&self, user_id: UserId) -> Option<usize> {
        self.state_mut().queue.shuffle(user_id)
    }

    /// The number of votes to skip the current song
    pub fn vote_count(&self) -> usize {
        self.state().skip_votes.len()
    }

    pub fn vote_to_skip(&self, user_id: UserId)  {
        self.state_mut().skip_votes.insert(user_id);
    }

    pub async fn play_next(&self) -> Result<Option<TrackInfo>> {
        let (previous, playing) = {
            let mut state = self.state_mut();
            state.skip_votes.clear();
            let previous = state.currently_playing.as_ref().map(|kv| kv.1.clone());
            state.currently_playing = match state.queue.pop() {
                Some(kv) => {
                    get_lavalink_player!(self).send(kv.value.play(self.0.guild_id))?;
                    Some((kv.key, kv.value.info))
                },
                None => None
            };
            (previous, state.currently_playing.clone())
        };
        if playing.is_none() {
            self.disconnect().await?;
        }
        Ok(previous)
    }

    pub async fn connect(&self, channel_id: ChannelId) -> Result<()> {
        let gateway = &self.0.gateway;
        let shard_id = gateway.shard_id(self.0.guild_id);
        gateway
            .command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": channel_id,
                    "guild_id": self.0.guild_id,
                    "self_mute": false,
                    "self_deaf": false,
                }
            }))
            .await?;
        self.state_mut().channel_id = Some(channel_id);
        info!("Connected to channel {} in guild {}", channel_id, self.0.guild_id);
        Ok(())
    }

    pub async fn disconnect(&self) -> Result<()> {
        let gateway = &self.0.gateway;
        let shard_id = gateway.shard_id(self.0.guild_id);
        gateway
            .command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": None::<ChannelId>,
                    "guild_id": self.0.guild_id,
                    "self_mute": false,
                    "self_deaf": false,
                }
            }))
            .await?;
        get_lavalink_player!(self).send(Destroy::from(self.0.guild_id))?;
        self.state_mut().channel_id = None;
        info!("Turning down player for guild {}", self.0.guild_id);
        if let Some(manager) = self.0.manager.upgrade() {
            manager.destroy_player(self.0.guild_id);
        }
        Ok(())
    }

    pub fn set_pause(&self, paused: bool) -> Result<()> {
        get_lavalink_player!(self).send(Pause::from((self.0.guild_id, paused)))?;
        Ok(())
    }

    pub fn set_volume(&self, volume: u8) -> Result<()> {
        get_lavalink_player!(self).send(Volume::from((self.0.guild_id, volume as i64)))?;
        Ok(())
    }

    pub async fn handle_event(&self, event: &IncomingEvent) -> Result<()> {
        match event {
            IncomingEvent::TrackStart(evt) => Ok(self.on_track_start(evt).await),
            IncomingEvent::TrackEnd(evt) => self.on_track_end(evt).await,
            _ => Ok(()),
        }
    }

    async fn on_track_start(&self, evt: &TrackStart) {
        info!("Track in guild {}: {}", self.0.guild_id, evt.track);
    }

    async fn on_track_end(&self, evt: &TrackEnd) -> Result<()> {
        info!("Track ended in guild {} (reason: {}): {}",
              self.0.guild_id, evt.reason.as_str(), evt.track);
        match evt.reason.as_str() {
            "FINISHED" => {self.play_next().await?;}
            "LOAD_FAILED" => {self.play_next().await?;}
            _ => {},
        }
        Ok(())
    }

}
