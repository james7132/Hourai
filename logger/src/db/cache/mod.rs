mod compression;
mod guild_config;
mod keys;
mod protobuf;

use crate::prelude::*;
use crate::proto::{
    cache::*,
};
use self::keys::{Id, CacheKey, CachePrefix};
use self::protobuf::Protobuf;
use twilight_model::id::*;

pub struct OnlineStatus {
    pipeline: redis::Pipeline
}

impl Default for OnlineStatus {
    fn default() -> Self {
        Self { pipeline: redis::pipe().atomic().clone() }
    }
}

impl OnlineStatus {

    pub fn new() -> Self {
        Self::default()
    }

    pub fn set_online(&mut self, guild_id: GuildId, online: impl IntoIterator<Item=UserId>)
                      -> &mut Self {
        let key = CacheKey(CachePrefix::OnlineStatus, guild_id.0);
        let ids: Vec<Id<u64>> = online.into_iter().map(|id| Id(id.0)).collect();
        self.pipeline
            .del(key).ignore()
            .sadd(key, ids).ignore()
            .expire(key, 3600);
        self
    }

    pub fn build(self) -> redis::Pipeline {
        self.pipeline
    }

}

pub struct CachedMessage {
    proto: Protobuf<CachedMessageProto>,
}

impl CachedMessage {

    pub fn new(message: twilight_model::channel::Message) -> Self {
        let mut msg = CachedMessageProto::new();
        msg.set_id(message.id.0);
        msg.set_channel_id(message.channel_id.0);
        msg.set_content(message.content);
        if let Some(guild_id) = message.guild_id {
            msg.set_guild_id(guild_id.0)
        }

        let user = msg.mut_author();
        let author = &message.author;
        user.set_id(author.id.0);
        user.set_username(author.name.clone());
        user.set_discriminator(message.author.discriminator() as u32);

        Self {
            proto: Protobuf(msg)
        }
    }

    pub fn flush(self) -> redis::Pipeline {
        let channel_id = self.proto.0.get_channel_id();
        let id = self.proto.0.get_id();
        let key = CacheKey(CachePrefix::Messages, (channel_id, id));
        let mut pipeline = redis::pipe();
        pipeline.atomic().set(key, self.proto).expire(key, 3600);
        pipeline
    }

    pub fn delete(channel_id: ChannelId, id: MessageId) -> redis::Cmd {
        Self::bulk_delete(channel_id, vec![id])
    }

    pub fn bulk_delete(
        channel_id: ChannelId,
        ids: impl IntoIterator<Item=MessageId>
    ) -> redis::Cmd {
        let keys: Vec<CacheKey<(u64, u64)>> =
            ids.into_iter()
               .map(|id| CacheKey(CachePrefix::Messages, (channel_id.0, id.0)))
               .collect();
        redis::Cmd::del(keys)
    }

}
