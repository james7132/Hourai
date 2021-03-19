use crate::Client;
use anyhow::anyhow;
use anyhow::Result;
use chrono::Utc;
use hourai::models::gateway::payload::{MessageDelete, MessageDeleteBulk};
use hourai::models::id::*;
use hourai::models::{MessageLike, Snowflake, UserLike};
use hourai::proto::guild_configs::*;
use hourai::proto::util::IdFilter;
use hourai_redis::{CachedMessage, GuildConfig};
use twilight_embed_builder::*;

fn message_base_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    let author = message.author();
    Ok(EmbedBuilder::new()
        .footer(
            EmbedFooterBuilder::new(format!("{} ({})", author.display_name(), author.id()))?
                .icon_url(ImageSource::url(author.avatar_url())?),
        )
        .title(format!("ID: {}", message.id()))?
        .url(message.message_link())
        .timestamp(Utc::now().to_rfc3339()))
}

fn message_to_embed(message: &impl MessageLike) -> Result<EmbedBuilder> {
    Ok(message_base_embed(message)?.description(message.content())?)
}

fn message_diff_embed(before: &impl MessageLike, after: &impl MessageLike) -> Result<EmbedBuilder> {
    Ok(message_base_embed(before)?
        .field(EmbedFieldBuilder::new("Before", before.content())?)
        .field(EmbedFieldBuilder::new("After", after.content())?))
}

async fn get_logging_config(client: &mut Client, guild_id: GuildId) -> Result<LoggingConfig> {
    Ok(GuildConfig::fetch_or_default(guild_id, &mut client.redis).await?)
}

fn meets_id_filter(filter: &IdFilter, id: u64) -> bool {
    if filter.denylist.contains(&id) {
        return false;
    }
    if filter.allowlist.len() > 0 && filter.allowlist.contains(&id) {
        return true;
    }
    return true;
}

fn should_log(config: &MessageLoggingConfig, channel_id: ChannelId) -> bool {
    config.get_enabled() && meets_id_filter(config.get_channel_filter(), channel_id.0)
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
    Some(ChannelId(id))
}

pub(super) async fn on_message_update(
    mut client: Client,
    before: impl MessageLike,
    after: impl MessageLike,
) -> Result<()> {
    if before.author().bot() {
        return Ok(());
    }
    let guild_id = before.guild_id().ok_or_else(|| anyhow!("Not in guild."))?;
    let config = get_logging_config(&mut client, guild_id).await?;
    let type_config = config.get_edited_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, before.channel_id()) {
        client
            .http_client
            .create_message(output_channel.unwrap())
            .content(format!(
                "Message by <@{}> edited from <#{}>",
                before.id(),
                before.channel_id()
            ))?
            .embed(
                message_diff_embed(&before, &after)?
                    .color(0xa84300)? // Dark orange
                    .build()?,
            )?
            .await?;
    }
    Ok(())
}

pub(super) async fn on_message_delete(client: &mut Client, evt: &MessageDelete) -> Result<()> {
    let guild_id = evt.guild_id.ok_or_else(|| anyhow!("Not in guild."))?;
    let config = get_logging_config(client, guild_id).await?;
    let type_config = config.get_deleted_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, evt.channel_id) {
        let cached = CachedMessage::fetch(evt.channel_id, evt.id, &mut client.redis).await?;
        if let Some(msg) = cached {
            if msg.author().bot() {
                return Ok(());
            }
            client
                .http_client
                .create_message(output_channel.unwrap())
                .content(format!(
                    "Message by <@{}> deleted from <#{}>",
                    msg.author().get_id(),
                    msg.get_channel_id()
                ))?
                .embed(
                    message_to_embed(&msg)?
                        .color(0x992d22)? // Dark red
                        .build()?,
                )?
                .await?;
        }
    }
    Ok(())
}

pub(super) async fn on_message_bulk_delete(
    mut client: Client,
    evt: MessageDeleteBulk,
) -> Result<()> {
    let guild_id = evt.guild_id.ok_or_else(|| anyhow!("Not in guild."))?;
    let config = get_logging_config(&mut client, guild_id).await?;
    let type_config = config.get_deleted_messages();
    let output_channel = get_output_channel(&config, type_config);
    if output_channel.is_some() && should_log(type_config, evt.channel_id) {
        client
            .http_client
            .create_message(output_channel.unwrap())
            .content(format!(
                "{} messages bulk deleted from <#{}>",
                evt.ids.len(),
                evt.channel_id
            ))?
            .await?;
    }
    Ok(())
}
