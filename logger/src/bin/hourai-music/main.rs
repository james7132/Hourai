mod prelude;
mod queue;
mod player;
mod track;

use anyhow::bail;
use crate::{prelude::*, player::{Player, PlayerManager}, track::Track};
use hourai::{config, commands, init, cache::{InMemoryCache, ResourceType}};
use twilight_model::{channel::Message, id::ChannelId};
use twilight_lavalink::{Lavalink, http::LoadType};
use twilight_command_parser::{Parser, CommandParserConfig, Command};
use twilight_gateway::{
    Intents, Event, EventTypeFlags,
    cluster::*,
};
use std::{convert::TryFrom, str::FromStr};
use hyper::{
    Body, Request,
    service::Service,
    client::{
        Client as HyperClient, HttpConnector,
        connect::dns::{Name, GaiResolver}
    }
};

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_MESSAGES.bits() |
    Intents::GUILD_VOICE_STATES.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::READY.bits() |
        EventTypeFlags::VOICE_STATE_UPDATE.bits() |
        EventTypeFlags::GUILD_DELETE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(
        ResourceType::VOICE_STATE.bits() |
        ResourceType::USER_CURRENT.bits());

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    let initializer = init::Initializer::new(config.clone());

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

    let http_client = initializer.http_client();
    let current_user = http_client.current_user().await.unwrap();
    let cache = InMemoryCache::builder().resource_types(CACHED_RESOURCES).build();
    let gateway = Cluster::builder(&config.discord.bot_token, BOT_INTENTS)
            .shard_scheme(ShardScheme::Auto)
            .http_client(http_client.clone())
            .build()
            .await
            .expect("Failed to connect to the Discord gateway");

    let shard_count = gateway.config().shard_config().shard()[1];
    let lavalink = Lavalink::new(current_user.id, shard_count);
    let client = Client {
        http_client,
        lavalink: lavalink.clone(),
        cache: cache.clone(),
        gateway: gateway.clone(),
        players: Arc::new(PlayerManager::new()),
        hyper: HyperClient::new(),
        resolver: GaiResolver::new(),
        parser: parser
    };

    // Start the lavalink node connections.
    for node in config.music.nodes {
        tokio::spawn(client.clone().run_node(node));
    }

    info!("Starting gateway...");
    gateway.up().await;
    info!("Client started.");

    let mut events = gateway.some_events(BOT_EVENTS);
    while let Some((_, evt)) = events.next().await {
        cache.update(&evt);
        if let Err(err) = lavalink.process(&evt).await {
            error!("Error while handling Lavalink event: {}", err);
        }
        tokio::spawn(client.clone().consume_event(evt));
    }

    info!("Shutting down gateway...");
    gateway.down();
    info!("Client stopped.");
}

#[derive(Clone)]
pub struct Client<'a> {
    pub http_client: twilight_http::Client,
    pub hyper: HyperClient<HttpConnector>,
    pub gateway: Cluster,
    pub cache: InMemoryCache,
    pub lavalink: twilight_lavalink::Lavalink,
    pub players: Arc<PlayerManager>,
    pub resolver: GaiResolver,
    pub parser: Parser<'a>
}

impl Client<'static> {

    pub fn user_id(&self) -> UserId {
        self.cache
            .current_user()
            .expect("Bot is not ready yet.")
            .id
    }

    async fn connect_node(
        &mut self,
        uri: impl AsRef<str>,
        password: impl Into<String>
    ) -> Result<LavalinkEventStream> {
        let name = Name::from_str(uri.as_ref()).unwrap();
        let pass = password.into();
        for address in self.resolver.call(name).await? {
            debug!("Trying to connect to a Lavalink node at: {} ", address);
            match self.lavalink.add(address, pass.as_str()).await  {
                Ok((_, rx)) => return Ok(rx),
                Err(err) => debug!("Failed to connect to {}: {:?}", address, err)
            }
        }
        bail!("No valid destination. Cannot connect.");
    }

    async fn run_node(mut self, config: config::MusicNode) {
        let name = format!("https://{}:{}", config.host, config.port);
        info!("Starting listener for node {}.", name.as_str());
        loop {
            let connect = self.connect_node(name.as_str(), config.password.as_str());
            let mut rx: LavalinkEventStream = match connect.await {
                Ok(rx) => rx,
                Err(err) => {
                    error!("Error connecting to node {}: {:?}", name.as_str(), err);
                    debug!("Retrying connection to {} in 5 seconds.", name.as_str());
                    tokio::time::sleep(Duration::from_secs(5)).await;
                    continue;
                },
            };

            info!("Connected to node to {}.", name.as_str());
            while let Some(event) = rx.next().await {
                tokio::spawn(self.clone().handle_lavalink_event(event));
            }
            info!("Disconnected from node to ({}).", name.as_str());
        }
    }

    async fn consume_event(self, event: Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::MessageCreate(evt) => self.on_message_create(evt.0).await,
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.players.destroy_player(evt.id);
                }
                Ok(())
            },
            Event::VoiceStateUpdate(_) => Ok(()),
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            },
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {:?}", kind, err);
        }
    }

    async fn handle_lavalink_event(self, event: IncomingEvent) {
        let guild_id = match &event {
            IncomingEvent::PlayerUpdate(evt) => evt.guild_id,
            IncomingEvent::TrackStart(evt) => evt.guild_id,
            IncomingEvent::TrackEnd(evt) => evt.guild_id,
            _ => return,
        };

        let result = if let Some(player) = self.players.get_player(guild_id) {
            player.handle_event(&event).await
        } else {
            error!("Recieved unexpected lavalink event: {:?}", &event);
            return;
        };

        if let Err(err) = result {
            error!("Error while handling Lavalink event {:?}. Error: {:?}", event, err);
        }
    }

    pub async fn get_lavalink_node(&self, guild_id: GuildId)
        -> Result<twilight_lavalink::Node> {
        Ok(match self.lavalink.players().get(&guild_id) {
            Some(kv) => kv.value().node().clone(),
            None => self.lavalink.best().await?,
        })
    }

    // HTTP requests to the Lavalink nodes
    pub async fn load_tracks(
        &self,
        node: Node,
        query: impl AsRef<str>
    ) -> Result<LoadedTracks> {
        let config = node.config().clone();
        let (parts, body) = twilight_lavalink::http::load_track(
                config.address,
                query.as_ref(),
                &config.authorization,
            )?
            .into_parts();
        let req = Request::from_parts(parts, Body::from(body));
        let res = self.hyper.request(req).await?;
        let response_bytes = hyper::body::to_bytes(res.into_body()).await?;
        Ok(serde_json::from_slice::<LoadedTracks>(&response_bytes)?)
    }

    fn require_in_voice_channel(&self, ctx: &commands::Context<'_>)
        -> Result<Option<ChannelId>> {
        let guild_id = commands::precondition::require_in_guild(&ctx)?;

        let user = self.cache.voice_state(guild_id, ctx.message.author.id);
        let bot = self.cache.voice_state(guild_id, self.user_id());
        if user.is_none() {
            bail!(CommandError::FailedPrecondition(
                  "You must be in a voice channel to play music."));
        } else if bot.is_some() && user != bot {
            bail!(CommandError::FailedPrecondition(
                  "You must be in the same voice channel to play music."));
        }

        Ok(user)
    }

    fn require_playing(&self, ctx: &commands::Context<'_>) -> Result<Player> {
        let guild_id = commands::precondition::require_in_guild(&ctx)?;

        self.players
            .get_player(guild_id)
            .ok_or_else(||
                CommandError::FailedPrecondition("No music is currently playing.").into())
    }

    async fn require_dj(&self, ctx: &commands::Context<'_>) -> Result<()> {
        self.require_in_voice_channel(&ctx)?;
        Ok(())
    }

    // Commands
    async fn on_message_create(self, evt: Message) -> Result<()> {
        if let Some(command) = self.parser.parse(evt.content.as_str()) {
            let ctx = commands::Context {
                message: &evt,
                http: self.http_client.clone(),
                cache: self.cache.clone(),
            };

            let result = match command {
                Command { name: "play", arguments, .. } =>
                    self.play(ctx, arguments.into_remainder()).await,
                Command { name: "pause", .. } => self.pause(ctx, true).await,
                Command { name: "stop", .. } => self.stop(ctx).await,
                Command { name: "shuffle",  .. } => self.shuffle(ctx).await,
                Command { name: "skip", .. } => self.skip(ctx).await,
                Command { name: "forceskip", .. } => self.forceskip(ctx).await,
                Command { name: "remove", arguments, .. } => Ok(()),
                Command { name: "removeall", .. } => self.remove_all(ctx).await,
                Command { name: "nowplaying", .. } => Ok(()),
                Command { name: "np", .. } => Ok(()),
                Command { name: "queue", .. } => Ok(()),
                Command { name: "volume", arguments, .. } =>
                    // TODO(james7132): Do proper argument parsing.
                    self.volume(ctx, 100).await,
                _ => Ok(())
            };

            if let Err(err) = result {
                match err.downcast::<CommandError>() {
                    Ok(command_error) => {
                        self.http_client
                            .create_message(evt.channel_id)
                            .reply(evt.id)
                            .content(format!(":x: {}", command_error))?
                            .await?;
                    },
                    Err(err) => bail!(err),
                }
            }
        }
        Ok(())
    }

    async fn play(&self, ctx: commands::Context<'_>, query: Option<&str>) -> Result<()> {
        let guild_id = commands::precondition::require_in_guild(&ctx)?;
        let user_channel_id = self.require_in_voice_channel(&ctx)?;

        if query.is_none() {
            return self.pause(ctx, false).await;
        }

        let node = self.get_lavalink_node(guild_id).await?;
        let response = match self.load_tracks(node, query.unwrap()).await {
            Ok(tracks) => tracks,
            Err(_) => bail!(CommandError::GenericFailure("Failed to load track(s).")),
        };

        let tracks = match response {
            LoadedTracks { load_type: LoadType::TrackLoaded, tracks, .. } => {
                assert!(tracks.len() > 0);
                vec![tracks[0].clone()]
            },
            LoadedTracks { load_type: LoadType::SearchResult, tracks, .. } => {
                // TODO(james7132): This could be improved by doing a edit distance from
                // the query for similarity matching.
                assert!(tracks.len() > 0);
                vec![tracks[0].clone()]
            },
            LoadedTracks { load_type: LoadType::PlaylistLoaded, tracks, playlist_info, .. } => {
                if let Some(idx) = playlist_info.selected_track {
                    vec![tracks[idx as usize].clone()]
                } else {
                    tracks
                }
            },
            LoadedTracks { load_type: LoadType::LoadFailed, .. } => {
                bail!(CommandError::GenericFailure("Failed to load tracks."));
            },
            _ => vec![],
        };

        let queue: Vec<Track> = tracks.into_iter()
                                      .filter_map(|t| Track::try_from(t).ok())
                                      .collect();
        let duration = format_duration(queue.iter().map(|t| t.info.length).sum());

        let response = if queue.len() > 1 {
            format!(":notes: Added **{}** tracks ({}) to the music queue.",
                    queue.len(), duration)
        } else if queue.len() == 1 {
            format!(":notes: Added `{}` ({}) to the music queue.",
                    &queue[0].info, duration)
        } else {
            format!(":bulb: No results found for `{}`", query.unwrap())
        };

        if let Some(player) = self.players.get_player(guild_id) {
            player.enqueue(ctx.message.author.id, queue);
        } else {
            let player = Player::new(&self, guild_id).await?;
            player.enqueue(ctx.message.author.id, queue);
            player.connect(user_channel_id.unwrap()).await?;
            player.play_next().await?;
        }

        ctx.respond().content(response)?.await?;
        Ok(())
    }

    async fn pause(&self, ctx: commands::Context<'_>, pause: bool) -> Result<()> {
        self.require_dj(&ctx).await?;
        self.require_playing(&ctx)?.set_pause(pause)?;
        Ok(())
    }

    async fn stop(&self, ctx: commands::Context<'_>) -> Result<()> {
        self.require_dj(&ctx).await?;
        self.require_playing(&ctx)?
            .disconnect()
            .await?;
        ctx.respond()
           .content("The player has been stopped and the queue has been cleared")?
           .await?;
        Ok(())
    }

    async fn skip(&self, ctx: commands::Context<'_>) -> Result<()> {
        self.require_in_voice_channel(&ctx)?;
        let player = self.require_playing(&ctx)?;
        player.vote_to_skip(ctx.message.author.id);

        // TODO(james7132): Make this ratio configurable.
        let listeners = self.cache.voice_channel_users(player.channel_id().unwrap()).len();
        let votes_required = listeners / 2;

        let response = if player.vote_count() >= votes_required {
            format!("Skipped `{}`", player.play_next().await?.unwrap())
        } else {
            format!("Total votes: `{}/{}`.", player.vote_count(), votes_required)
        };

        ctx.respond().content(response)?.await?;
        Ok(())
    }

    // TODO(james7132): Properly implmement
    async fn remove(&self, ctx: commands::Context<'_>, index: usize) -> Result<()> {
        self.require_in_voice_channel(&ctx)?;
        let player = self.require_playing(&ctx)?;
        ctx.respond().content("Skipped.")?.await?;
        Ok(())
    }

    async fn remove_all(&self, ctx: commands::Context<'_>) -> Result<()> {
        self.require_in_voice_channel(&ctx)?;
        let player = self.require_playing(&ctx)?;
        let response = if let Some(count) = player.clear_user(ctx.message.author.id) {
            format!("Removed **{}** tracks from the queue.", count)
        } else {
            "You currently do not have any tracks in the queue.".to_owned()
        };
        ctx.respond().content(response)?.await?;
        Ok(())
    }

    async fn shuffle(&self, ctx: commands::Context<'_>) -> Result<()> {
        self.require_in_voice_channel(&ctx)?;
        let player = self.require_playing(&ctx)?;
        let response = if let Some(count) = player.shuffle(ctx.message.author.id) {
            format!("Shuffled **{}** tracks in the queue.", count)
        } else {
            "You currently do not have any tracks in the queue.".to_owned()
        };
        ctx.respond().content(response)?.await?;
        Ok(())
    }

    async fn forceskip(&self, ctx: commands::Context<'_>) -> Result<()> {
        self.require_dj(&ctx).await?;
        let player = self.require_playing(&ctx)?;
        let response = if let Some(previous) = player.play_next().await? {
            format!("Skipped `{}`.", previous)
        } else {
            "There is nothing in the queue right now.".to_owned()
        };
        ctx.respond().content(response)?.await?;
        Ok(())
    }

    async fn volume(&self, ctx: commands::Context<'_>, volume: i64) -> Result<()> {
        self.require_dj(&ctx).await?;
        if volume < 0 || volume > 150 {
            bail!(CommandError::InvalidArgument(
                    "Volume must be between 0 and 150.".into()));
        }
        self.require_playing(&ctx)?.set_volume(volume)?;
        ctx.respond().content(format!("Set volume to `{}`.", volume))?.await?;
        Ok(())
    }

    async fn queue(&self, _: commands::Context<'_>) -> Result<()> {
        // TODO(james7132): Implement.
        Ok(())
    }

    async fn now_playing(&self, _: commands::Context<'_>) -> Result<()> {
        // TODO(james7132): Implement.
        Ok(())
    }
}

fn format_duration(duration: Duration) -> String {
    let mut secs = duration.as_secs();
    let hours = secs / 3600;
    secs -= hours * 3600;
    let minutes = secs / 60;
    secs -= secs * 60;
    if hours > 0 {
        format!("{:02}:{:02}:{:02}", hours, minutes, secs)
    } else {
        format!("{:02}:{:02}", minutes, secs)
    }
}

