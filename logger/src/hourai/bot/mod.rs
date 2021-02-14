mod background_job;
mod event_handler;
mod utils;

use futures::StreamExt;
use twilight_gateway::{Cluster, Intents, EventTypeFlags};
use twilight_gateway::cluster::{ClusterStartError, ShardScheme};

use crate::config::HouraiConfig;
pub use self::event_handler::EventHandler;

const BOT_INTENTS: Intents =
    Intents::from_bits_truncate(
        Intents::all().bits() &
        // TODO(james7132): Find a way to enable GUILD_PRESENCES without
        // blowing up the memory usage.
        !Intents::GUILD_PRESENCES.bits() &
        !Intents::GUILD_MESSAGE_TYPING.bits() &
        !Intents::GUILD_INTEGRATIONS.bits()  &
        !Intents::GUILD_WEBHOOKS.bits()  &
        !Intents::DIRECT_MESSAGE_TYPING.bits()  &
        !Intents::DIRECT_MESSAGE_REACTIONS.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::MEMBER_UPDATE.bits() |
        EventTypeFlags::MESSAGE_CREATE.bits());

#[derive(Clone)]
pub struct Client {
    pub gateway: Cluster,
    pub standby: twilight_standby::Standby,
    event_handler: EventHandler,
}

impl Client {
    pub async fn new(config: &HouraiConfig, event_handler: EventHandler)
        -> std::result::Result<Self, ClusterStartError> {
        Ok(Self {
            gateway: Cluster::builder(&config.discord.bot_token, BOT_INTENTS)
                .shard_scheme(ShardScheme::Auto)
                .build()
                .await?,
            standby: twilight_standby::Standby::new(),
            event_handler: event_handler,
        })
    }

    pub fn http_client(&self) -> &twilight_http::Client {
        return &self.gateway.config().http_client();
    }

    pub async fn run(&self) {
        self.gateway.up().await;

        let mut events = self.gateway.some_events(BOT_EVENTS);
        while let Some((shard_id, evt)) = events.next().await {
            self.standby.process(&evt);
            self.event_handler.consume_event(shard_id, evt);
        }

        self.gateway.down();
    }
}
