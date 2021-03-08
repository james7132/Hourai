use anyhow::Result;
use crate::models::{Snowflake, MessageLike, UserLike};
use twilight_embed_builder::image_source::ImageSource;
use chrono::offset::Utc;
use twilight_embed_builder::*;

pub fn message_link(message: &impl MessageLike) -> String {
    let prefix = if let Some(guild_id) = message.guild_id() {
        guild_id.to_string()
    } else {
        "@me".to_owned()
    };
    format!("https://discord.com/channels/{}/{}/{}", prefix, message.channel_id(), message.id())
}

pub fn message_base_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    let author = message.author();
    Ok(EmbedBuilder::new()
        .footer(EmbedFooterBuilder::new(format!("{} ({})", author.display_name(), author.id()))?
            .icon_url(ImageSource::url(author.avatar_url())?))
        .title(format!("ID: {}", message.id()))?
        .url(message_link(message))
        .timestamp(Utc::now().to_rfc3339()))
}

pub fn message_to_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    Ok(message_base_embed(message)?.description(message.content())?)
}

pub fn message_diff_embed(
    before: &impl MessageLike,
    after: &impl MessageLike
) -> Result<EmbedBuilder> {
    Ok(message_base_embed(before)?
        .field(EmbedFieldBuilder::new("Before", before.content())?)
        .field(EmbedFieldBuilder::new("After", after.content())?))
}
