use hourai::{prelude::*, init, cache::{InMemoryCache, ResourceType}};
use twilight_model::channel::Message;
use twilight_lavalink::{Lavalink, model::IncomingEvent};
use twilight_gateway::{
    Intents, Event, EventTypeFlags,
    cluster::*,
};
use hyper::client::{Client as HyperClient, HttpConnector};

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_MESSAGES.bits() |
    Intents::GUILD_VOICE_STATES.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::VOICE_STATE_UPDATE.bits() |
        EventTypeFlags::GUILD_DELETE.bits() |
        EventTypeFlags::PRESENCE_UPDATE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(
        ResourceType::VOICE_STATE.bits() |
        ResourceType::USER_CURRENT.bits());

pub async fn run(initializer: init::Initializer) {
    Client::new(initializer).await.run().await;
}

#[derive(Clone)]
pub struct Client {
    pub http_client: twilight_http::Client,
    pub hyper: HyperClient<HttpConnector>,
    pub gateway: Cluster,
    pub cache: InMemoryCache,
    pub lavalink: twilight_lavalink::Lavalink,
}

impl Client {

    pub async fn new(initializer: init::Initializer) -> Self {
        let config = initializer.config();
        let http_client = initializer.http_client();
        let current_user = http_client.current_user().await.unwrap();
        let gateway = Cluster::builder(&config.discord.bot_token, BOT_INTENTS)
                .shard_scheme(ShardScheme::Auto)
                .http_client(http_client.clone())
                .build()
                .await
                .expect("Failed to connect to the Discord gateway");

        let shard_count = gateway.config().shard_config().shard()[1];

        Self {
            http_client: http_client,
            hyper: HyperClient::new(),
            lavalink: Lavalink::new(current_user.id, shard_count),
            gateway: gateway,
            cache: InMemoryCache::builder()
                .resource_types(CACHED_RESOURCES)
                .build(),
        }
    }

    pub async fn run(&self) {
        info!("Starting gateway...");
        self.gateway.up().await;
        info!("Client started.");

        let mut events = self.gateway.some_events(BOT_EVENTS);
        while let Some((shard_id, evt)) = events.next().await {
            self.cache.update(&evt);
            self.lavalink.process(&evt).await;
            tokio::spawn(self.clone().consume_event(shard_id, evt));
        }

        info!("Shutting down gateway...");
        self.gateway.down();
        info!("Client stopped.");
    }

    async fn consume_event(self, shard_id: u64, event: Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::MessageCreate(evt) => self.on_message_create(evt.0).await,
            Event::VoiceStateUpdate(evt) => Ok(()),
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            },
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {:?}", kind, err);
        }
    }

    fn handle_lavalink_event(event: &IncomingEvent) {
        match event {
            IncomingEvent::TrackStart(evt) => {
                info!("Track started in guild {}: {}", evt.guild_id, evt.track);
            },
            IncomingEvent::TrackEnd(evt) => {
                info!("Track ended in guild {} ({}): {}", evt.guild_id, evt.reason, evt.track);
            },
            _ => {}
        }
    }

    // Commands

    async fn on_message_create(self, evt: Message) -> Result<()> {
        Ok(())
    }

    async fn play(self) -> Result<()> {
        Ok(())
    }

    async fn pause(self) -> Result<()> {
        Ok(())
    }

    async fn stop(self) -> Result<()> {
        Ok(())
    }

    async fn skip(self) -> Result<()> {
        Ok(())
    }

    async fn forceskip(self) -> Result<()> {
        Ok(())
    }

    async fn queue(self) -> Result<()> {
        Ok(())
    }

    async fn now_playing(self) -> Result<()> {
        Ok(())
    }

}
