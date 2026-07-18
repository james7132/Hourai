use super::prelude::*;
use anyhow::Result;
use hourai::{
    models::guild::Permissions,
    proto::guild_configs::{AnnouncementConfig, LoggingConfig, MusicConfig},
};

pub async fn reddit_add(_ctx: &CommandContext) -> Result<Response> {
    anyhow::bail!(InteractionError::NotImplemented);
}

pub async fn reddit_remove(_ctx: &CommandContext) -> Result<Response> {
    anyhow::bail!(InteractionError::NotImplemented);
}

pub async fn reddit_list(_ctx: &CommandContext) -> Result<Response> {
    anyhow::bail!(InteractionError::NotImplemented);
}

pub async fn setdj(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let role_id = ctx.get_role("role")?;
    let mut config: MusicConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.set_dj_role_id(vec![role_id.get()]);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;
    Ok(Response::direct().content(format!(
        "Set <@&{}> as the DJ role for this server.",
        role_id
    )))
}

pub async fn log_edited(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let mut config: LoggingConfig = storage.redis().guild(guild_id).configs().get().await?;
    let edited = config.mut_edited_messages();
    let enabled = !edited.get_enabled();
    edited.set_enabled(enabled);
    edited.set_output_channel_id(ctx.channel_id().get());
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    let state = if enabled { "enabled" } else { "disabled" };
    Ok(Response::direct().content(format!(
        "Logging of edited messages has been {} in <#{}>.",
        state,
        ctx.channel_id()
    )))
}

pub async fn log_deleted(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let mut config: LoggingConfig = storage.redis().guild(guild_id).configs().get().await?;
    let deleted = config.mut_deleted_messages();
    let enabled = !deleted.get_enabled();
    deleted.set_enabled(enabled);
    deleted.set_output_channel_id(ctx.channel_id().get());
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    let state = if enabled { "enabled" } else { "disabled" };
    Ok(Response::direct().content(format!(
        "Logging of deleted messages has been {} in <#{}>.",
        state,
        ctx.channel_id()
    )))
}

pub async fn setmodlog(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let channel_id = ctx
        .get_channel("channel")
        .unwrap_or_else(|_| ctx.channel_id());
    let mut config: LoggingConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.set_modlog_channel_id(channel_id.get());
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    Ok(Response::direct().content(format!("Set <#{}> as the modlog channel.", channel_id)))
}

pub async fn announce_join(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let channel_id = ctx
        .get_channel("channel")
        .unwrap_or_else(|_| ctx.channel_id());
    let mut config: AnnouncementConfig = storage.redis().guild(guild_id).configs().get().await?;
    let joins = config.mut_joins();
    let mut ids = joins.get_channel_ids().to_vec();
    let state = if ids.contains(&channel_id.get()) {
        ids.retain(|&id| id != channel_id.get());
        "disabled"
    } else {
        ids.push(channel_id.get());
        "enabled"
    };
    joins.set_channel_ids(ids);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;
    Ok(Response::direct().content(format!(
        "Join announcements {} in <#{}>.",
        state, channel_id
    )))
}

pub async fn announce_leave(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let channel_id = ctx
        .get_channel("channel")
        .unwrap_or_else(|_| ctx.channel_id());
    let mut config: AnnouncementConfig = storage.redis().guild(guild_id).configs().get().await?;
    let leaves = config.mut_leaves();
    let mut ids = leaves.get_channel_ids().to_vec();
    let state = if ids.contains(&channel_id.get()) {
        ids.retain(|&id| id != channel_id.get());
        "disabled"
    } else {
        ids.push(channel_id.get());
        "enabled"
    };
    leaves.set_channel_ids(ids);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;
    Ok(Response::direct().content(format!(
        "Leave announcements {} in <#{}>.",
        state, channel_id
    )))
}

pub async fn announce_ban(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let channel_id = ctx
        .get_channel("channel")
        .unwrap_or_else(|_| ctx.channel_id());
    let mut config: AnnouncementConfig = storage.redis().guild(guild_id).configs().get().await?;
    let bans = config.mut_bans();
    let mut ids = bans.get_channel_ids().to_vec();
    let state = if ids.contains(&channel_id.get()) {
        ids.retain(|&id| id != channel_id.get());
        "disabled"
    } else {
        ids.push(channel_id.get());
        "enabled"
    };
    bans.set_channel_ids(ids);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;
    Ok(Response::direct().content(format!("Ban announcements {} in <#{}>.", state, channel_id)))
}

pub async fn announce_voice(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let channel_id = ctx
        .get_channel("channel")
        .unwrap_or_else(|_| ctx.channel_id());
    let mut config: AnnouncementConfig = storage.redis().guild(guild_id).configs().get().await?;
    let voice = config.mut_voice();
    let mut ids = voice.get_channel_ids().to_vec();
    let state = if ids.contains(&channel_id.get()) {
        ids.retain(|&id| id != channel_id.get());
        "disabled"
    } else {
        ids.push(channel_id.get());
        "enabled"
    };
    voice.set_channel_ids(ids);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;
    Ok(Response::direct().content(format!(
        "Voice announcements {} in <#{}>.",
        state, channel_id
    )))
}
