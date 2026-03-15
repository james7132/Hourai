use crate::Client;
use anyhow::Result;
use hourai::{
    models::{
        guild::{Member, Permissions},
        id::{marker::*, Id},
        RoleFlags,
    },
    proto::{cache::CachedRoleProto, guild_configs::*},
};
use hourai_storage::Storage;
use std::collections::HashMap;

async fn get_roles(
    storage: &Storage,
    guild_id: Id<GuildMarker>,
    user_id: Id<UserMarker>,
) -> hourai_sql::Result<Vec<Id<RoleMarker>>> {
    hourai_sql::Member::fetch(guild_id, user_id)
        .fetch_one(storage.sql())
        .await
        .map(|member| member.role_ids().collect())
}

async fn get_role_flags(
    storage: &Storage,
    guild_id: Id<GuildMarker>,
) -> Result<HashMap<u64, RoleFlags>> {
    let config: RoleConfig = storage.redis().guild(guild_id).configs().get().await?;
    Ok(config
        .get_settings()
        .iter()
        .map(|(k, v)| (*k, RoleFlags::from_bits_truncate(v.get_flags())))
        .collect())
}

async fn get_verification_role(
    storage: &Storage,
    guild_id: Id<GuildMarker>,
) -> Result<Option<Id<RoleMarker>>> {
    let config: VerificationConfig = storage.redis().guild(guild_id).configs().get().await?;

    if config.get_enabled() && config.has_role_id() {
        Ok(Some(Id::new(config.get_role_id())))
    } else {
        Ok(None)
    }
}

pub async fn on_member_join(client: &Client, member: &Member) -> Result<()> {
    let guild_id = member.guild_id;
    let user_id = member.user.id;

    let bot_roles = match get_roles(client.storage(), guild_id, client.user_id()).await {
        Ok(roles) => roles,
        Err(hourai_sql::Error::RowNotFound) => return Ok(()),
        Err(err) => anyhow::bail!(err),
    };

    let redis = client.storage().redis();
    let mut guild = redis.guild(guild_id);
    let perms = guild
        .guild_permissions(client.user_id(), bot_roles.iter().cloned())
        .await?;
    if !perms.contains(Permissions::MANAGE_ROLES) {
        return Ok(());
    }

    let user_roles = match get_roles(client.storage(), guild_id, user_id).await {
        Ok(roles) => guild.role_set(&roles).await?,
        Err(hourai_sql::Error::RowNotFound) => return Ok(()),
        Err(err) => anyhow::bail!(err),
    };

    let max_role = guild
        .role_set(&bot_roles)
        .await?
        .highest()
        .cloned()
        .unwrap_or_else(|| CachedRoleProto::default());

    let flags = get_role_flags(client.storage(), guild_id).await?;
    let mut restorable: Vec<Id<RoleMarker>> = user_roles
        .iter()
        .filter(|role| {
            let role_flags = flags
                .get(&role.get_role_id())
                .cloned()
                .unwrap_or_else(RoleFlags::empty);
            role < &&max_role && role_flags.contains(RoleFlags::RESTORABLE)
        })
        .map(|role| Id::new(role.get_role_id()))
        .collect();

    // Do not give out the verification role if it is enabled.
    if let Some(role) = get_verification_role(client.storage(), guild_id).await? {
        restorable.retain(|id| *id != role);
    }

    if restorable.is_empty() {
        return Ok(());
    }

    client
        .http()
        .update_guild_member(guild_id, member.user.id)
        .roles(&restorable)
        .await?;

    Ok(())
}
