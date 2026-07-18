use crate::RedisClient;
use anyhow::Result;
use hourai::{
    http::{Client, request::channel::message::create_message::CreateMessage},
    models::id::{Id, marker::*},
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

    pub async fn get_guild_modlog(&self, guild_id: Id<GuildMarker>) -> Result<Modlog> {
        let config: LoggingConfig = self.redis.guild(guild_id).configs().get().await?;

        Ok(Modlog {
            http: self.http.clone(),
            channel_id: Id::new(config.get_modlog_channel_id()),
        })
    }
}

#[derive(Clone)]
pub struct Modlog {
    http: Arc<Client>,
    channel_id: Id<ChannelMarker>,
}

impl Modlog {
    pub fn create_message(&self) -> CreateMessage<'_> {
        self.http.create_message(self.channel_id)
    }
}
