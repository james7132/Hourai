use super::user::UserLike;
use super::Snowflake;
use crate::proto::cache::{CachedMessageProto, CachedUserProto};
use twilight_model::channel::Attachment;
use twilight_model::channel::message::{embed::Embed, Message};
use twilight_model::gateway::payload::incoming::MessageUpdate;
use twilight_model::id::{marker::*, Id};
use twilight_model::user::User;

pub trait MessageLike: Snowflake<Id<MessageMarker>> {
    type Author: UserLike;

    fn channel_id(&self) -> Id<ChannelMarker>;
    fn guild_id(&self) -> Option<Id<GuildMarker>>;
    fn author(&self) -> &Self::Author;
    fn content(&self) -> &str;
    fn embeds(&self) -> &[Embed];
    fn attachments(&self) -> &[Attachment];

    /// Gets the link to the message
    fn message_link(&self) -> String {
        let prefix = if let Some(guild_id) = self.guild_id() {
            guild_id.to_string()
        } else {
            "@me".to_owned()
        };
        format!(
            "https://discord.com/channels/{}/{}/{}",
            prefix,
            self.channel_id(),
            self.id()
        )
    }
}

impl Snowflake<Id<MessageMarker>> for Message {
    fn id(&self) -> Id<MessageMarker> {
        self.id
    }
}

impl Snowflake<Id<MessageMarker>> for CachedMessageProto {
    fn id(&self) -> Id<MessageMarker> {
        Id::new(self.get_id())
    }
}

impl Snowflake<Id<MessageMarker>> for MessageUpdate {
    fn id(&self) -> Id<MessageMarker> {
        self.id
    }
}

impl MessageLike for Message {
    type Author = User;

    fn channel_id(&self) -> Id<ChannelMarker> {
        self.channel_id
    }

    fn guild_id(&self) -> Option<Id<GuildMarker>> {
        self.guild_id
    }

    fn author(&self) -> &User {
        &self.author
    }

    fn content(&self) -> &str {
        self.content.as_str()
    }

    fn embeds(&self) -> &[Embed] {
        &self.embeds
    }

    fn attachments(&self) -> &[Attachment] {
        &self.attachments
    }
}

impl MessageLike for MessageUpdate {
    type Author = User;

    fn channel_id(&self) -> Id<ChannelMarker> {
        self.channel_id
    }

    fn guild_id(&self) -> Option<Id<GuildMarker>> {
        self.guild_id
    }

    fn author(&self) -> &User {
        self.author.as_ref().unwrap()
    }

    fn content(&self) -> &str {
        self.content.as_deref().unwrap()
    }

    fn embeds(&self) -> &[Embed] {
        self.embeds.as_deref().unwrap()
    }

    fn attachments(&self) -> &[Attachment] {
        self.attachments.as_deref().unwrap()
    }
}

impl MessageLike for CachedMessageProto {
    type Author = CachedUserProto;

    fn channel_id(&self) -> Id<ChannelMarker> {
        Id::new(self.get_channel_id())
    }

    fn guild_id(&self) -> Option<Id<GuildMarker>> {
        if self.has_guild_id() {
            Some(Id::new(self.get_guild_id()))
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

    // TODO(james7132): Implement this fix this
    fn embeds(&self) -> &[Embed] {
        &[]
    }

    fn attachments(&self) -> &[Attachment] {
        &[]
    }
}
