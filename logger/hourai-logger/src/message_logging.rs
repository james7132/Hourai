use crate::Client;
use anyhow::anyhow;
use anyhow::Result;
use chrono::Utc;
use hourai::models::{
    datetime::Timestamp,
    gateway::payload::incoming::{MessageDelete, MessageDeleteBulk},
    id::*,
    MessageLike, Snowflake, UserLike,
};
use hourai::proto::guild_configs::*;
use hourai::proto::util::IdFilter;
use twilight_embed_builder::*;

fn message_base_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    let author = message.author();
    Ok(EmbedBuilder::new()
        .footer(
            EmbedFooterBuilder::new(format!("{} ({})", author.display_name(), author.id()))
                .icon_url(ImageSource::url(author.avatar_url())?),
        )
        .title(format!("ID: {}", message.id()))
        .url(message.message_link())
        .timestamp(Timestamp::from_secs(Utc::now().timestamp() as u64).unwrap()))
}

pub(crate) fn message_to_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    Ok(message_base_embed(message)?.description(message.content()))
}

pub(crate) fn message_diff_embed(
    before: &impl MessageLike,
    after: &impl MessageLike,
) -> Result<EmbedBuilder> {
    Ok(message_base_embed(before)?
        .field(EmbedFieldBuilder::new("Before", before.content()))
        .field(EmbedFieldBuilder::new("After", after.content())))
}

fn meets_id_filter(filter: &IdFilter, id: u64) -> bool {
    if filter.denylist.contains(&id) {
        return false;
    }
    if !filter.allowlist.is_empty() && filter.allowlist.contains(&id) {
        return true;
    }
    return true;
}

fn should_log(config: &MessageLoggingConfig, channel_id: ChannelId) -> bool {
    config.get_enabled() && meets_id_filter(config.get_channel_filter(), channel_id.get())
}

fn get_output_channel(
    config: &LoggingConfig,
    type_config: &MessageLoggingConfig,
) -> Option<ChannelId> {
    let id = if config.has_modlog_channel_id() {
        config.get_modlog_channel_id()
    } else if type_config.has_output_channel_id() {
        type_config.get_output_channel_id()
    } else {
        return None;
    };
    ChannelId::new(id)
}

pub(super) async fn on_message_update(
    client: Client,
    before: impl MessageLike,
    after: impl MessageLike,
) -> Result<()> {
    if before.author().bot() {
        return Ok(());
    }
    let guild_id = before.guild_id().ok_or_else(|| anyhow!("Not in guild."))?;
    let redis = client.storage().redis();
    let config: LoggingConfig = redis.guild(guild_id).configs().get().await?;
    let type_config = config.get_edited_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, before.channel_id()) {
        client
            .http()
            .create_message(output_channel.unwrap())
            .content(&format!(
                "Message by <@{}> edited from <#{}>",
                before.author().id(),
                before.channel_id()
            ))?
            .embeds(&vec![message_diff_embed(&before, &after)?
                .color(0xa84300) // Dark orange
                .build()?])?
            .exec()
            .await?;
    }
    Ok(())
}

pub(super) async fn on_message_delete(client: &mut Client, evt: &MessageDelete) -> Result<()> {
    let guild_id = evt.guild_id.ok_or_else(|| anyhow!("Not in guild."))?;
    let redis = client.storage().redis();
    let config: LoggingConfig = redis.guild(guild_id).configs().get().await?;
    let type_config = config.get_deleted_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, evt.channel_id) {
        let cached = redis.messages().fetch(evt.channel_id, evt.id).await?;
        if let Some(msg) = cached {
            if msg.author().bot() {
                return Ok(());
            }
            client
                .http()
                .create_message(output_channel.unwrap())
                .content(&format!(
                    "Message by <@{}> deleted from <#{}>",
                    msg.author().get_id(),
                    msg.get_channel_id()
                ))?
                .embeds(&vec![message_to_embed(&msg)?
                    .color(0x992d22) // Dark red
                    .build()?])?
                .exec()
                .await?;
        }
    }
    Ok(())
}

pub(super) async fn on_message_bulk_delete(client: Client, evt: MessageDeleteBulk) -> Result<()> {
    let guild_id = evt.guild_id.ok_or_else(|| anyhow!("Not in guild."))?;
    let redis = client.storage().redis();
    let config: LoggingConfig = redis.guild(guild_id).configs().get().await?;
    let type_config = config.get_deleted_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, evt.channel_id) {
        client
            .http()
            .create_message(output_channel.unwrap())
            .content(&format!(
                "{} messages bulk deleted from <#{}>",
                evt.ids.len(),
                evt.channel_id
            ))?
            .exec()
            .await?;
    }
    Ok(())
}
