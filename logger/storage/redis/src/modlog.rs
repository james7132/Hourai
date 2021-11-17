use crate::RedisClient;
use anyhow::Result;
use hourai::{
    http::{request::channel::message::create_message::CreateMessage, Client},
    models::id::{ChannelId, GuildId},
    proto::guild_configs::LoggingConfig,
};
use std::sync::Arc;

#[derive(Clone)]
pub struct ModLogger {
    http: Arc<Client>,
    redis: RedisClient,
}

impl ModLogger {
    pub fn new(http: Arc<Client>, redis: RedisClient) -> Self {
        Self { http, redis }
    }

    pub async fn get_guild_modlog(&self, guild_id: GuildId) -> Result<Option<Modlog>> {
        let config: LoggingConfig = self
            .redis
            .guild_configs()
            .fetch_or_default(guild_id)
            .await?;

        Ok(
            ChannelId::new(config.get_modlog_channel_id()).map(|id| Modlog {
                http: self.http.clone(),
                channel_id: id,
            }),
        )
    }
}

#[derive(Clone)]
pub struct Modlog {
    http: Arc<Client>,
    channel_id: ChannelId,
}

impl Modlog {
    pub fn create_message(&self) -> CreateMessage {
        self.http.create_message(self.channel_id)
    }
}
