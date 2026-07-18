use super::prelude::*;
use anyhow::Result;
use chrono::{Duration, Utc};
use hourai::http::request::AuditLogReason;
use hourai::models::{
    guild::Permissions,
    id::{marker::*, Id},
};
use hourai::proto::guild_configs::VerificationConfig;
use twilight_util::builder::embed::*;

pub async fn setup(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let role_id = ctx.get_role("role").ok();
    let mut config: VerificationConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.set_enabled(true);
    if let Some(role) = role_id {
        config.set_role_id(role.get());
    } else {
        config.clear_role_id();
    }
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    Ok(Response::direct().content("Verification configuration enabled."))
}

pub async fn disable(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let mut config: VerificationConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.set_enabled(false);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    Ok(Response::direct().content("Verification disabled."))
}

pub async fn verify(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let user_id = ctx.get_user("user")?;
    let member = actions
        .http()
        .guild_member(guild_id, user_id)
        .await?
        .model()
        .await?;

    let verifiers = crate::verification::make_verifiers(
        hourai::cache::InMemoryCache::new(),
        actions.storage().sql().clone(),
    );
    let verify_ctx = crate::verification::verify_member(&member, &verifiers).await?;

    let mut desc = format!("**User:** <@{}>\n", user_id);
    if verify_ctx.is_approved() {
        desc.push_str("\n**Status:** ✅ Approved");
    } else {
        desc.push_str("\n**Status:** ❌ Rejected\n\n**Reasons:**\n");
        for r in verify_ctx.rejection_reasons() {
            desc.push_str(&format!("• {}\n", r));
        }
    }

    let embed = EmbedBuilder::new()
        .title("Verification Check Results")
        .description(desc)
        .color(if verify_ctx.is_approved() {
            0x57F287
        } else {
            0xED4245
        })
        .build();

    Ok(Response::direct().embed(embed))
}

pub async fn purge(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Kick Members"));
    }

    let config: VerificationConfig = actions
        .storage()
        .redis()
        .guild(guild_id)
        .configs()
        .get()
        .await?;
    if !config.get_enabled() || !config.has_role_id() {
        return Ok(Response::ephemeral()
            .content("Verification is not enabled or no verification role is configured."));
    }

    let role_id = Id::<RoleMarker>::new(config.get_role_id());
    let lookback_hours = ctx.get_int("lookback_hours").unwrap_or(6);
    let cutoff = Utc::now() - Duration::hours(lookback_hours);

    let members = actions.storage().sql().clone();
    let local_members = hourai_sql::Member::find_with_roles(guild_id, vec![])
        .fetch_all(&members)
        .await?;

    let mut purged = 0;
    for mem in local_members {
        if mem.bot || mem.role_ids.contains(&(role_id.get() as i64)) {
            continue;
        }
        let user_id = Id::<UserMarker>::new(mem.user_id as u64);
        if let Ok(m) = actions.http().guild_member(guild_id, user_id).await {
            if let Ok(guild_member) = m.model().await {
                let joined_secs = guild_member.joined_at.as_secs();
                if joined_secs < cutoff.timestamp() {
                    let reason = "Unverified user purged.";
                    if let Ok(del) = actions
                        .http()
                        .remove_guild_member(guild_id, user_id)
                        .reason(reason)
                    {
                        if del.await.is_ok() {
                            purged += 1;
                        }
                    }
                }
            }
        }
    }

    Ok(Response::direct().content(format!(
        "Purged {} unverified users who joined more than {} hours ago.",
        purged, lookback_hours
    )))
}

pub async fn lockdown(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let duration_hours = ctx.get_int("hours").unwrap_or(1);
    let expiration = Utc::now() + Duration::hours(duration_hours);
    let mut config: VerificationConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.set_lockdown_expiration(expiration.timestamp() as u64);
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    Ok(Response::direct().content(format!("Lockdown enabled for {} hour(s).", duration_hours)))
}

pub async fn lockdown_lift(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_GUILD) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Server"));
    }
    let mut config: VerificationConfig = storage.redis().guild(guild_id).configs().get().await?;
    config.clear_lockdown_expiration();
    storage
        .redis()
        .guild(guild_id)
        .configs()
        .set(config)
        .await?;

    Ok(Response::direct().content("Lockdown lifted."))
}

pub async fn propagate(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_ROLES) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Roles"));
    }
    let config: VerificationConfig = actions
        .storage()
        .redis()
        .guild(guild_id)
        .configs()
        .get()
        .await?;
    if !config.has_role_id() {
        return Ok(Response::ephemeral().content("No verification role configured."));
    }
    let role_id = Id::<RoleMarker>::new(config.get_role_id());

    let members = actions.storage().sql().clone();
    let local_members = hourai_sql::Member::find_with_roles(guild_id, vec![])
        .fetch_all(&members)
        .await?;

    let mut added = 0;
    for mem in local_members {
        if mem.bot || mem.role_ids.contains(&(role_id.get() as i64)) {
            continue;
        }
        let user_id = Id::<UserMarker>::new(mem.user_id as u64);
        if let Ok(add) = actions
            .http()
            .add_guild_member_role(guild_id, user_id, role_id)
            .reason("Propagating verification role.")
        {
            if add.await.is_ok() {
                added += 1;
            }
        }
    }

    Ok(Response::direct().content(format!(
        "Propagated verification role to {} member(s).",
        added
    )))
}
