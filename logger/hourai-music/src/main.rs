mod buttons;
mod interaction_ui;
mod interactions;
mod player;
mod prelude;
mod queue;
mod track;

use crate::{
    player::PlayerState,
    prelude::*,
    queue::MusicQueue,
    track::{Track, TrackInfo},
};
use anyhow::{bail, Result};
use dashmap::DashMap;
use futures::stream::StreamExt;
use hourai::{
    config,
    gateway::{cluster::*, Event, EventTypeFlags, Intents},
    init,
    models::{
        application::interaction::{Interaction, InteractionType},
        gateway::payload::outgoing::UpdateVoiceState,
        guild::Guild,
        http::interaction::*,
        id::{marker::*, Id},
    },
    proto::{guild_configs::MusicConfig, music_bot::MusicStateProto},
};
use hourai_redis::*;
use http::Uri;
use hyper::{
    client::{
        connect::dns::{GaiResolver, Name},
        Client as HyperClient, HttpConnector,
    },
    service::Service,
    Body, Request,
};
use std::{convert::TryFrom, str::FromStr};
use twilight_lavalink::{model::*, Lavalink};

const RESUME_KEY: &str = "MUSIC";

const BOT_INTENTS: Intents =
    Intents::from_bits_truncate(Intents::GUILDS.bits() | Intents::GUILD_VOICE_STATES.bits());

const BOT_EVENTS: EventTypeFlags = EventTypeFlags::from_bits_truncate(
    EventTypeFlags::CHANNEL_CREATE.bits()
        | EventTypeFlags::CHANNEL_DELETE.bits()
        | EventTypeFlags::CHANNEL_UPDATE.bits()
        | EventTypeFlags::GUILD_CREATE.bits()
        | EventTypeFlags::GUILD_DELETE.bits()
        | EventTypeFlags::INTERACTION_CREATE.bits()
        | EventTypeFlags::READY.bits()
        | EventTypeFlags::VOICE_SERVER_UPDATE.bits()
        | EventTypeFlags::VOICE_STATE_UPDATE.bits(),
);

#[tokio::main]
async fn main() {
    let config = config::load_config();
    init::init(&config);

    let http_client = Arc::new(init::http_client(&config));
    let redis = hourai_redis::init(&config).await;
    let sessions = redis.resume_states().get_sessions(RESUME_KEY).await;
    let (gateway, mut events) = init::cluster(&config, BOT_INTENTS)
        .http_client(http_client.clone())
        .event_types(BOT_EVENTS)
        .resume_sessions(sessions)
        .build()
        .await
        .expect("Failed to connect to the Discord gateway");
    let gateway = Arc::new(gateway);
    let current_user = http_client
        .current_user()
        .await
        .unwrap()
        .model()
        .await
        .unwrap();
    let lavalink = Arc::new(Lavalink::new(
        current_user.id,
        gateway.shards().len() as u64,
    ));
    let client = Client {
        user_id: current_user.id,
        http_client,
        lavalink: lavalink.clone(),
        gateway: gateway.clone(),
        states: Arc::new(DashMap::new()),
        hyper: HyperClient::new(),
        redis: redis.clone(),
    };

    // Start the lavalink node connections.
    for node in config.music.nodes {
        tokio::spawn(client.clone().run_node(node));
    }

    info!("Starting gateway...");
    gateway.up().await;
    info!("Client started.");

    loop {
        tokio::select! {
            _ = tokio::signal::ctrl_c() =>  { break; }
            res = events.next() => {
                if let Some((_, evt)) = res {
                    if let Err(err) = lavalink.process(&evt).await {
                        error!("Error while handling Lavalink event: {}", err);
                    }
                    tokio::spawn(client.clone().consume_event(evt));
                } else {
                    break;
                }
            }
        }
    }

    info!("Shutting down gateway...");
    let result = redis
        .resume_states()
        .save_sessions(RESUME_KEY, gateway.down_resumable())
        .await;
    if let Err(err) = result {
        tracing::error!("Error while shutting down cluster: {} ({:?})", err, err);
    }
    info!("Client stopped.");
}

#[derive(Clone)]
pub struct Client {
    pub user_id: Id<UserMarker>,
    pub http_client: Arc<hourai::http::Client>,
    pub hyper: HyperClient<HttpConnector>,
    pub gateway: Arc<Cluster>,
    pub lavalink: Arc<twilight_lavalink::Lavalink>,
    pub states: Arc<DashMap<Id<GuildMarker>, PlayerState>>,
    pub redis: RedisClient,
}

impl Client {
    async fn connect_node(
        &mut self,
        uri: &Uri,
        password: impl Into<String>,
    ) -> Result<IncomingEvents> {
        let name = Name::from_str(uri.host().unwrap()).unwrap();
        let pass = password.into();
        let mut resolver = GaiResolver::new();
        for mut address in resolver.call(name).await? {
            if let Some(port) = uri.port_u16() {
                address.set_port(port);
            }

            debug!("Trying to connect to a Lavalink node at: {} ", address);
            match self.lavalink.add(address, pass.as_str()).await {
                Ok((_, rx)) => return Ok(rx),
                Err(err) => debug!("Failed to connect to {}: {:?}", address, err),
            }
        }
        bail!("No valid destination. Cannot connect.");
    }

    async fn run_node(mut self, config: config::MusicNode) {
        let name = format!("http://{}:{}", config.host, config.port);
        let uri = Uri::try_from(name.as_str()).unwrap();
        info!("Starting listener for node {}.", name.as_str());
        loop {
            let connect = self.connect_node(&uri, config.password.as_str());
            let mut rx: IncomingEvents = match connect.await {
                Ok(rx) => rx,
                Err(err) => {
                    error!("Error connecting to node {}: {:?}", name.as_str(), err);
                    debug!("Retrying connection to {} in 5 seconds.", name.as_str());
                    tokio::time::sleep(Duration::from_secs(5)).await;
                    continue;
                }
            };

            info!("Connected to node to {}.", name.as_str());
            while let Some(event) = rx.next().await {
                tokio::spawn(self.clone().handle_lavalink_event(event));
            }
            info!("Disconnected from node to ({}).", name.as_str());
        }
    }

    async fn consume_event(self, event: Event) {
        let kind = event.kind();
        let result = match event {
            Event::ChannelCreate(_) => Ok(()),
            Event::ChannelDelete(_) => Ok(()),
            Event::ChannelUpdate(_) => Ok(()),
            Event::GuildCreate(evt) => self.on_guild_create(evt.0).await,
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.disconnect(evt.id).await
                } else {
                    Ok(())
                }
            }
            Event::InteractionCreate(evt) => self.on_interaction_create(evt.0).await,
            Event::Ready(_) => Ok(()),
            Event::VoiceStateUpdate(_) => Ok(()),
            Event::VoiceServerUpdate(_) => Ok(()),
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            }
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {:?}", kind, err);
        }
    }

    async fn on_guild_create(self, evt: Guild) -> Result<()> {
        // Do not load state if there is already an existing player for a guild.
        if self.states.contains_key(&evt.id) {
            return Ok(());
        }

        let guild = self.redis.guild(evt.id);
        let exists: std::result::Result<bool, _> = guild.music_queue().has_saved_state().await;
        if exists.unwrap_or(false) {
            // The bot will not leave the channel when it restarts, it should still be in the voice
            // channel.
            let channel_id = guild.voice_states().get_channel(self.user_id).await?;
            if let Some(channel_id) = channel_id {
                self.states.insert(evt.id, PlayerState::default());
                let state = self.load_state(evt.id).await?;
                self.connect(evt.id, channel_id).await?;
                if state.has_position() {
                    self.start_playing(evt.id, Some(state.get_position()))
                        .await?;
                } else {
                    self.start_playing(evt.id, None).await?;
                }
                tracing::info!(
                    "Bot was already in voice channel {} in guild {}. Restored session.",
                    channel_id,
                    evt.id
                );
            } else {
                guild.music_queue().clear().await?;
            }
        }

        Ok(())
    }

    async fn on_interaction_create(self, evt: Interaction) -> Result<()> {
        match evt.kind {
            InteractionType::Ping => {
                self.http_client
                    .interaction(evt.application_id)
                    .create_response(
                        evt.id,
                        &evt.token,
                        &InteractionResponse {
                            kind: InteractionResponseType::Pong,
                            data: None,
                        },
                    )
                    .await?;
            }
            InteractionType::ApplicationCommand => {
                let ctx = hourai::interactions::CommandContext::new(
                    self.http_client.clone(),
                    evt
                );
                interactions::handle_command(self, ctx).await?;
            }
            InteractionType::MessageComponent => {
                let ctx = hourai::interactions::ComponentContext::new(
                    self.http_client.clone(),
                    evt,
                );
                interactions::handle_component(self, ctx).await?;
            }
            interaction => {
                warn!("Unknown incoming interaction: {:?}", interaction);
                return Ok(());
            }
        };
        Ok(())
    }

    async fn handle_lavalink_event(self, event: IncomingEvent) {
        let result = match &event {
            IncomingEvent::TrackStart(ref evt) => {
                info!("Started track in guild {}: {}", evt.guild_id, evt.track);
                Ok(())
            }
            IncomingEvent::TrackEnd(evt) => self.on_track_end(evt).await,
            IncomingEvent::PlayerUpdate(evt) => self.save_state(evt.guild_id).await,
            _ => Ok(()),
        };

        if let Err(err) = result {
            error!(
                "Error while handling Lavalink event {:?}. Error: {:?}",
                event, err
            );
        }
    }

    async fn on_track_end(&self, evt: &TrackEnd) -> Result<()> {
        info!(
            "Track ended in guild {} (reason: {}): {}",
            evt.guild_id,
            evt.reason.as_str(),
            evt.track
        );
        match evt.reason.as_str() {
            "FINISHED" => {
                self.play_next(evt.guild_id).await?;
            }
            "LOAD_FAILED" => {
                self.play_next(evt.guild_id).await?;
            }
            _ => {}
        }
        Ok(())
    }

    /// Gets the music config for a server.
    pub async fn get_config(&self, guild_id: Id<GuildMarker>) -> Result<MusicConfig> {
        let config: MusicConfig = self.redis.guild(guild_id).configs().get().await?;
        Ok(config)
    }

    /// Sets the music config for the sever.
    pub async fn set_config(&self, guild_id: Id<GuildMarker>, config: MusicConfig) -> Result<()> {
        self.redis.guild(guild_id).configs().set(config).await?;
        Ok(())
    }

    pub async fn save_state(&self, guild_id: Id<GuildMarker>) -> Result<()> {
        let state = self
            .states
            .get(&guild_id)
            .map(|kv| kv.value().save_to_proto());
        let mut queue = self.redis.guild(guild_id).music_queue();
        if let Some(mut state) = state {
            let position = self
                .lavalink
                .players()
                .get(&guild_id)
                .map(|kv| kv.position());
            if let Some(position) = position {
                state.set_position(position);
            }
            queue.save(state).await?;
        } else {
            queue.clear().await?;
        }
        tracing::debug!("Saved player state for guild {}", guild_id);
        Ok(())
    }

    pub async fn load_state(&self, guild_id: Id<GuildMarker>) -> Result<MusicStateProto> {
        let state = self.redis.guild(guild_id).music_queue().load().await?;
        self.mutate_state(guild_id, |player| {
            player.load_from_proto(state.clone());
        });
        tracing::debug!("Loaded player state for guild {}", guild_id);
        Ok(state)
    }

    /// Gets some information about a guild's player queue.
    pub fn get_queue<F, R>(&self, guild_id: Id<GuildMarker>, f: F) -> Option<R>
    where
        F: Fn(&MusicQueue<Id<UserMarker>, Track>) -> R,
    {
        self.states.get(&guild_id).map(|kv| f(&kv.value().queue))
    }

    /// Gets the currently playing track in a given guild.
    /// If not playing, return None.
    pub fn currently_playing(&self, guild_id: Id<GuildMarker>) -> Option<Track> {
        self.states
            .get(&guild_id)
            .and_then(|kv| kv.value().currently_playing().map(|cp| cp.1))
    }

    /// Gets the currently displayed queue page in a given guild.
    /// If not playing, return None.
    pub fn queue_page(&self, guild_id: Id<GuildMarker>) -> Option<i64> {
        self.states.get(&guild_id).map(|kv| kv.value().queue_page)
    }

    /// Gets which voice channel the bot is currently connected to in
    /// a guild.
    pub fn get_channel(&self, guild_id: Id<GuildMarker>) -> Option<Id<ChannelMarker>> {
        self.lavalink
            .players()
            .get(&guild_id)
            .and_then(|p| p.channel_id())
    }

    /// Counts the number of users in the same voice channel as the bot.
    /// If not in a voice channel, returns 0.
    pub async fn count_listeners(&self, guild_id: Id<GuildMarker>) -> Result<usize> {
        Ok(if let Some(channel_id) = self.get_channel(guild_id) {
            let states = self
                .redis
                .guild(guild_id)
                .voice_states()
                .get_channels()
                .await?;
            states.into_iter().filter(|(_, v)| *v == channel_id).count()
        } else {
            0
        })
    }

    pub async fn get_node(
        &self,
        guild_id: Id<GuildMarker>,
    ) -> Result<Arc<twilight_lavalink::Node>> {
        Ok(match self.lavalink.players().get(&guild_id) {
            Some(kv) => kv.node().clone(),
            None => self.lavalink.best().await?,
        })
    }

    // HTTP requests to the Lavalink nodes
    pub async fn load_tracks(&self, node: &Node, query: &str) -> Result<LoadedTracks> {
        let config = node.config();
        let (parts, body) =
            twilight_lavalink::http::load_track(config.address, query, &config.authorization)?
                .into_parts();
        let req = Request::from_parts(parts, Body::from(body));
        let res = self.hyper.request(req).await?;
        let response_bytes = hyper::body::to_bytes(res.into_body()).await?;
        tracing::debug!(
            "Recieved response when loading tracks for query \"{}\": {:?}",
            query,
            response_bytes
        );
        Ok(serde_json::from_slice::<LoadedTracks>(&response_bytes)?)
    }

    async fn play(&self, guild_id: Id<GuildMarker>, track: &Track) -> Result<()> {
        self.lavalink.player(guild_id).await?.play(track)?;
        Ok(())
    }

    pub async fn start_playing(
        &self,
        guild_id: Id<GuildMarker>,
        position: Option<i64>,
    ) -> Result<()> {
        if let Some(track) = self.currently_playing(guild_id) {
            let config = self.get_config(guild_id).await?;
            let player = self.lavalink.player(guild_id).await?;
            let volume = if config.has_volume() {
                config.get_volume()
            } else {
                50
            };
            player.set_volume(volume)?;
            player.play(&track)?;
            if let Some(position) = position {
                player.seek(position)?;
            }
        }
        Ok(())
    }

    /// Plays the next item in the queue.
    /// Panics if a player does not exist.
    pub async fn play_next(&self, guild_id: Id<GuildMarker>) -> Result<Option<TrackInfo>> {
        let prev = {
            if let Some(mut kv) = self.states.get_mut(&guild_id) {
                let state = kv.value_mut();
                state.skip_votes.clear();
                state.queue.pop().map(|kv| kv.value.info)
            } else {
                return Ok(None);
            }
        };
        // Must be done seperately to avoid a deadlock.
        if let Some(track) = self.currently_playing(guild_id) {
            self.play(guild_id, &track).await?;
        } else {
            self.disconnect(guild_id).await?;
        }
        self.save_state(guild_id).await?;
        Ok(prev)
    }

    pub async fn connect(
        &self,
        guild_id: Id<GuildMarker>,
        channel_id: Id<ChannelMarker>,
    ) -> Result<()> {
        self.gateway
            .command(
                self.gateway.shard_id(guild_id),
                &UpdateVoiceState::new(
                    guild_id, channel_id, /* self_deaf */ false, /* self_mute */ false,
                ),
            )
            .await?;

        info!("Connected to channel {} in guild {}", channel_id, guild_id);
        Ok(())
    }

    pub async fn disconnect(&self, guild_id: Id<GuildMarker>) -> Result<()> {
        self.gateway
            .command(
                self.gateway.shard_id(guild_id),
                &UpdateVoiceState::new(
                    guild_id, None, /* self_deaf */ false, /* self_mute */ false,
                ),
            )
            .await?;
        info!("Disconnected from guild {}", guild_id);

        self.lavalink.players().destroy(guild_id)?;
        self.states.remove(&guild_id);
        info!("Destroyed player and removed state for guild {}", guild_id);
        Ok(())
    }

    pub fn mutate_state<F, R>(&self, guild_id: Id<GuildMarker>, f: F) -> Option<R>
    where
        F: FnOnce(&mut PlayerState) -> R,
    {
        self.states
            .get_mut(&guild_id)
            .map(|mut kv| f(kv.value_mut()))
    }
}
