mod background_job;
mod event_handler;
mod utils;

use futures::StreamExt;
use twilight_gateway::{Cluster, Intents};
use twilight_gateway::cluster::{ClusterStartError, ShardScheme};

use crate::config::HouraiConfig;
pub use self::event_handler::EventHandler;

// TODO(james7132): Find a way to enable GUILD_PRESENCES without
// blowing up the memory usage.
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_MEMBERS.bits());

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

    #[inline]
    pub fn http_client(&self) -> &twilight_http::Client {
        return &self.gateway.config().http_client();
    }

    pub async fn run(&self) {
        self.gateway.up().await;

        let mut events = self.gateway.some_events(EventHandler::BOT_EVENTS);
        while let Some((_, evt)) = events.next().await {
            self.standby.process(&evt);
            self.event_handler.consume_event(evt);
        }

        self.gateway.down();
    }
}
