mod buttons;
mod commands;
mod interaction_ui;
mod interactions;
mod player;
mod prelude;
mod queue;
mod track;
mod ui;

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
        application::{callback::InteractionResponse, interaction::Interaction},
        gateway::payload::outgoing::UpdateVoiceState,
        guild::Guild,
        id::*,
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
use std::{collections::HashMap, convert::TryFrom, str::FromStr};
use twilight_command_parser::{CommandParserConfig, Parser};
use twilight_lavalink::{model::*, Lavalink};

const RESUME_KEY: &str = "MUSIC";

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() | Intents::GUILD_MESSAGES.bits() | Intents::GUILD_VOICE_STATES.bits(),
);

const BOT_EVENTS: EventTypeFlags = EventTypeFlags::from_bits_truncate(
    EventTypeFlags::CHANNEL_CREATE.bits()
        | EventTypeFlags::CHANNEL_DELETE.bits()
        | EventTypeFlags::CHANNEL_UPDATE.bits()
        | EventTypeFlags::GUILD_CREATE.bits()
        | EventTypeFlags::GUILD_DELETE.bits()
        | EventTypeFlags::INTERACTION_CREATE.bits()
        | EventTypeFlags::MESSAGE_CREATE.bits()
        | EventTypeFlags::READY.bits()
        | EventTypeFlags::VOICE_SERVER_UPDATE.bits()
        | EventTypeFlags::VOICE_STATE_UPDATE.bits(),
);

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    init::init(&config);

    let parser = {
        let mut parser = CommandParserConfig::new();
        parser.add_prefix(config.command_prefix.clone());
        parser.add_command("play", false);
        parser.add_command("pause", false);
        parser.add_command("stop", false);
        parser.add_command("shuffle", false);
        parser.add_command("skip", false);
        parser.add_command("forceskip", false);
        parser.add_command("remove", false);
        parser.add_command("volume", false);
        parser.add_command("removeall", false);
        parser.add_command("nowplaying", false);
        parser.add_command("np", false);
        parser.add_command("queue", false);
        Parser::new(parser)
    };

    let http_client = Arc::new(init::http_client(&config));
    let mut redis = hourai_redis::init(&config).await;
    let sessions = ResumeState::get_sessions(RESUME_KEY, &mut redis).await;
    let (gateway, mut events) = init::cluster(&config, BOT_INTENTS)
        .shard_scheme(ShardScheme::Auto)
        .http_client(http_client.clone())
        .event_types(BOT_EVENTS)
        .resume_sessions(sessions)
        .build()
        .await
        .expect("Failed to connect to the Discord gateway");
    let gateway = Arc::new(gateway);
    let current_user = http_client
        .current_user()
        .exec()
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
        resolver: GaiResolver::new(),
        parser,
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
    let result = ResumeState::save_sessions(RESUME_KEY, gateway.down_resumable(), &mut redis).await;
    if let Err(err) = result {
        tracing::error!("Error while shutting down cluster: {} ({:?})", err, err);
    }
    info!("Client stopped.");
}

#[derive(Clone)]
pub struct Client<'a> {
    pub user_id: UserId,
    pub http_client: Arc<hourai::http::Client>,
    pub hyper: HyperClient<HttpConnector>,
    pub gateway: Arc<Cluster>,
    pub lavalink: Arc<twilight_lavalink::Lavalink>,
    pub states: Arc<DashMap<GuildId, PlayerState>>,
    pub resolver: GaiResolver,
    pub redis: RedisPool,
    pub parser: Parser<'a>,
}

impl Client<'static> {
    async fn connect_node(
        &mut self,
        uri: &Uri,
        password: impl Into<String>,
    ) -> Result<IncomingEvents> {
        let name = Name::from_str(uri.host().unwrap()).unwrap();
        let pass = password.into();
        for mut address in self.resolver.call(name).await? {
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
            Event::MessageCreate(evt) => commands::on_message_create(self, evt.0).await,
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
        if !self.states.contains_key(&evt.id) {
            return Ok(());
        }

        let mut conn = self.redis.clone();
        let exists: std::result::Result<bool, _> = hourai_redis::MusicQueue::has_saved_state(evt.id)
            .query_async(&mut conn)
            .await;
        if exists.unwrap_or(false) {
            self.states.insert(evt.id, PlayerState::new());
            let state = self.load_state(evt.id).await?;
            if let Some(channel_id) = ChannelId::new(state.get_channel_id()) {
                self.connect(evt.id, channel_id).await?;
                self.start_playing(evt.id).await?;
            } else {
                self.states.remove(&evt.id);
            }
        }

        Ok(())
    }

    async fn on_interaction_create(self, evt: Interaction) -> Result<()> {
        info!("Recieved interaction: {:?}", evt);
        match evt {
            Interaction::Ping(ping) => {
                self.http_client
                    .interaction_callback(ping.id, &ping.token, &InteractionResponse::Pong)
                    .exec()
                    .await?;
            }
            Interaction::ApplicationCommand(cmd) => {
                let ctx = hourai::interactions::CommandContext {
                    http: self.http_client.clone(),
                    command: cmd,
                };
                interactions::handle_command(self, ctx).await?;
            }
            Interaction::MessageComponent(component) => {
                let ctx = hourai::interactions::ComponentContext {
                    http: self.http_client.clone(),
                    component,
                };
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
    pub async fn get_config(&self, guild_id: GuildId) -> Result<MusicConfig> {
        let mut conn = self.redis.clone();
        let config: MusicConfig = GuildConfig::fetch_or_default(guild_id, &mut conn).await?;
        Ok(config)
    }

    /// Sets the music config for the sever.
    pub async fn set_config(&self, guild_id: GuildId, config: MusicConfig) -> Result<()> {
        let mut conn = self.redis.clone();
        GuildConfig::set::<MusicConfig>(guild_id, config)
            .query_async(&mut conn)
            .await?;
        Ok(())
    }

    pub async fn save_state(&self, guild_id: GuildId) -> Result<()> {
        let mut conn = self.redis.clone();
        let state = self.states.get(&guild_id).map(|kv| kv.value().save_to_proto());
        if let Some(mut state) = state {
            let channel_id = self.lavalink
                .players()
                .get(&guild_id)
                .map(|kv| kv.channel_id())
                .flatten();
            if let Some(channel_id) = channel_id {
                state.set_channel_id(channel_id.get());
            }
            hourai_redis::MusicQueue::save(guild_id, state)
                .query_async(&mut conn)
                .await?;
        } else {
            hourai_redis::MusicQueue::clear(guild_id)
                .query_async(&mut conn)
                .await?;
        }
        tracing::info!("Saved player state for guild {}", guild_id);
        Ok(())
    }

    pub async fn load_state(&self, guild_id: GuildId) -> Result<MusicStateProto> {
        let mut conn = self.redis.clone();
        let state = hourai_redis::MusicQueue::load(guild_id, &mut conn).await?;
        self.mutate_state(guild_id, |player| {
            player.load_from_proto(state.clone());
        });
        tracing::info!("Loaded player state for guild {}", guild_id);
        Ok(state)
    }

    /// Gets some information about a guild's player queue.
    pub fn get_queue<F, R>(&self, guild_id: GuildId, f: F) -> Option<R>
    where
        F: Fn(&MusicQueue<UserId, Track>) -> R,
    {
        self.states.get(&guild_id).map(|kv| f(&kv.value().queue))
    }

    /// Gets the currently playing track in a given guild.
    /// If not playing, return None.
    pub fn currently_playing(&self, guild_id: GuildId) -> Option<Track> {
        self.states
            .get(&guild_id)
            .and_then(|kv| kv.value().currently_playing().map(|cp| cp.1))
    }

    /// Gets the currently displayed queue page in a given guild.
    /// If not playing, return None.
    pub fn queue_page(&self, guild_id: GuildId) -> Option<i64> {
        self.states.get(&guild_id).map(|kv| kv.value().queue_page)
    }

    /// Gets which voice channel the bot is currently connected to in
    /// a guild.
    pub fn get_channel(&self, guild_id: GuildId) -> Option<ChannelId> {
        self.lavalink
            .players()
            .get(&guild_id)
            .and_then(|p| p.channel_id())
    }

    /// Counts the number of users in the same voice channel as the bot.
    /// If not in a voice channel, returns 0.
    pub async fn count_listeners(&self, guild_id: GuildId) -> Result<usize> {
        Ok(if let Some(channel_id) = self.get_channel(guild_id) {
            let mut redis = self.redis.clone();
            let states: HashMap<u64, u64> = hourai_redis::CachedVoiceState::get_channels(guild_id)
                .query_async(&mut redis)
                .await?;
            states
                .into_iter()
                .filter(|(_, v)| *v == channel_id.get())
                .count()
        } else {
            0
        })
    }

    pub async fn get_node(&self, guild_id: GuildId) -> Result<Arc<twilight_lavalink::Node>> {
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

    async fn play(&self, guild_id: GuildId, track: &Track) -> Result<()> {
        self.lavalink.player(guild_id).await?.play(track)?;
        Ok(())
    }

    pub async fn start_playing(&self, guild_id: GuildId) -> Result<()> {
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
        }
        Ok(())
    }

    /// Plays the next item in the queue.
    /// Panics if a player does not exist.
    pub async fn play_next(&self, guild_id: GuildId) -> Result<Option<TrackInfo>> {
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

    pub async fn connect(&self, guild_id: GuildId, channel_id: ChannelId) -> Result<()> {
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

    pub async fn disconnect(&self, guild_id: GuildId) -> Result<()> {
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

    pub fn mutate_state<F, R>(&self, guild_id: GuildId, f: F) -> Option<R>
    where
        F: FnOnce(&mut PlayerState) -> R,
    {
        self.states
            .get_mut(&guild_id)
            .map(|mut kv| f(kv.value_mut()))
    }
}
