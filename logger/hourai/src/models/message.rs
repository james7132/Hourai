use super::Snowflake;
use super::user::UserLike;
use crate::proto::cache::{CachedMessageProto, CachedUserProto};
use twilight_model::id::*;
use twilight_model::user::User;
use twilight_model::channel::Message;
use twilight_model::gateway::payload::MessageUpdate;

pub trait MessageLike : Snowflake<MessageId> {
    type Author: UserLike;

    fn channel_id(&self) -> ChannelId;
    fn guild_id(&self) -> Option<GuildId>;
    fn author(&self) -> &Self::Author;
    fn content(&self) -> &str;

    /// Gets the link to the message
    fn message_link(&self) -> String {
        let prefix = if let Some(guild_id) = self.guild_id() {
            guild_id.to_string()
        } else {
            "@me".to_owned()
        };
        format!("https://discord.com/channels/{}/{}/{}", prefix, self.channel_id(), self.id())
    }
}

impl Snowflake<MessageId> for Message {
    fn id(&self) -> MessageId {
        self.id
    }
}

impl Snowflake<MessageId> for CachedMessageProto {
    fn id(&self) -> MessageId {
        MessageId(self.get_id())
    }
}

impl Snowflake<MessageId> for MessageUpdate {
    fn id(&self) -> MessageId {
        self.id
    }
}

impl MessageLike for Message {
    type Author = User;

    fn channel_id(&self) -> ChannelId {
        self.channel_id
    }

    fn guild_id(&self) -> Option<GuildId> {
        self.guild_id
    }

    fn author(&self) -> &User {
        &self.author
    }

    fn content(&self) -> &str {
        self.content.as_str()
    }
}

impl MessageLike for CachedMessageProto {
    type Author = CachedUserProto;

    fn channel_id(&self) -> ChannelId {
        ChannelId(self.get_channel_id())
    }

    fn guild_id(&self) -> Option<GuildId> {
        if self.has_guild_id() {
            Some(GuildId(self.get_guild_id()))
        } else {
            None
        }
    }

    fn author(&self) -> &CachedUserProto {
        self.get_author()
    }

    fn content(&self) -> &str {
        self.get_content()
    }
}
