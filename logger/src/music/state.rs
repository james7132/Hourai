use crate::prelude::*;
use dashmap::DashSet;
use super::{Client, queue::MusicQueue, track::*};
use std::sync::RwLock;
use tokio::sync::mpsc;
use twilight_lavalink::{Lavalink, model::*, http::LoadedTracks};
use twilight_model::id::{UserId, GuildId, ChannelId};
use hyper::{Body, Request};

macro_rules! get_player {
    ($self:expr) => {
        $self.lavalink()
             .player($self.guild_id())
             .await
             .expect("Player is missing.")
             .value()
    };
}

struct PlayerStateRef {
    client: Client,
    currently_playing: RwLock<Option<(UserId, TrackInfo)>>,
    queue: RwLock<MusicQueue<UserId, Track>>,
    skip_votes: DashSet<UserId>,
    reciever: RwLock<mpsc::UnboundedReceiver<IncomingEvent>>,
}

#[derive(Clone)]
pub struct PlayerState(GuildId, Arc<PlayerStateRef>);

impl PlayerState {

    pub fn new(client: &Client, guild_id: GuildId) -> (Self, mpsc::UnboundedSender<IncomingEvent>) {
        let (tx, rx) = mpsc::unbounded_channel();

        let state = Self(guild_id, Arc::new(PlayerStateRef {
            client: client.clone(),
            currently_playing: RwLock::new(None),
            skip_votes: DashSet::new(),
            reciever: RwLock::new(rx),
            queue: RwLock::new(MusicQueue::new())
        }));

        (state, tx)
    }

    #[inline(always)]
    fn lavalink(&self) -> &Lavalink {
        &self.1.client.lavalink
    }

    #[inline(always)]
    pub fn guild_id(&self) -> GuildId {
        self.0
    }

    /// Queues up a track to be played.
    pub fn enqueue(&self, user_id: UserId, track: Track) {
        self.1.queue.write().unwrap().push(user_id, track);
    }

    /// Removes all of a user's tracks from the queue.
    pub fn clear_user(&self, user_id: UserId) {
        self.1.queue.write().unwrap().clear_key(user_id);
    }

    /// Shuffle's all of a user's tracks in the queue.
    pub fn shuffle(&self, user_id: UserId) {
        self.1.queue.write().unwrap().shuffle(user_id);
    }

    pub async fn vote_to_skip(&self, user_id: UserId) {
        let vote_count = self.1.skip_votes.insert(user_id);
        // TODO(james7132): Properly implement this check.
        if false {
            self.play_next().await;
        }
    }

    pub async fn run(&self, channel_id: ChannelId) -> Result<()> {
        if let Err(err) = self.connect(channel_id).await {
            error!("Error while connecting to channel {} in guild {}: {:?}",
                   channel_id, self.guild_id(), err);
            return Err(err);
        }

        info!("Connected to channel {} in guild {}", channel_id, self.guild_id());

        let mut rx = self.1.reciever.write().unwrap();
        while let Some(event) = rx.recv().await {
            match event {
                IncomingEvent::PlayerUpdate(evt) => {
                    if evt.op == Opcode::Destroy {
                        break;
                    }
                },
                IncomingEvent::TrackEnd(evt) => self.on_track_end(evt).await,
                _ => {},
            }
        }

        info!("Turning down player for guild {}", self.guild_id());
        Ok(())
    }

    async fn on_track_end(&self, evt: TrackEnd) {
        match evt.reason.as_ref() {
            "FINISHED" => self.play_next().await,
            "LOAD_FAILED" => self.play_next().await,
            _ => {},
        }
    }

    async fn play_next(&self) {
        let next_song = self.1.queue.write().unwrap().pop();
        self.1.skip_votes.clear();
        *self.1.currently_playing.write().unwrap() = if let Some(kv) = next_song {
            // Play next message
            let track_info = kv.value.info.clone();
            get_player!(self).send(kv.value.play(self.guild_id()));
            Some((kv.key, track_info))
        } else {
            // Queue empty,
            self.disconnect().await;
            None
        };
    }

    pub async fn connect(&self, channel_id: ChannelId) -> Result<()> {
        let gateway = &self.1.client.gateway;
        let shard_id = gateway.shard_id(self.guild_id());
        gateway
            .command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": channel_id,
                    "guild_id": self.guild_id(),
                    "self_mute": false,
                    "self_deaf": false,
                }
            }))
            .await?;
        Ok(())
    }

    pub async fn disconnect(&self) -> Result<()> {
        get_player!(self).send(Destroy::from(self.guild_id()));
        let gateway = &self.1.client.gateway;
        let shard_id = gateway.shard_id(self.guild_id());
        gateway
            .command(shard_id, &serde_json::json!({
                "op": 4,
                "d": {
                    "channel_id": None::<ChannelId>,
                    "guild_id": self.guild_id(),
                    "self_mute": false,
                    "self_deaf": false,
                }
            }))
        .await?;
        Ok(())
    }

    pub async fn load_track(&self, query: impl AsRef<str>) -> Result<LoadedTracks> {
        let config = get_player!(self).node().config().clone();
        let (parts, body) = twilight_lavalink::http::load_track(
                config.address,
                query.as_ref(),
                &config.authorization,
            )?
            .into_parts();
        let req = Request::from_parts(parts, Body::from(body));
        let res = self.1.client.hyper.request(req).await?;
        let response_bytes = hyper::body::to_bytes(res.into_body()).await?;
        Ok(serde_json::from_slice::<LoadedTracks>(&response_bytes)?)
    }

}
