#[macro_use]
extern crate delegate;

pub mod actions;
pub mod escalation;
mod storage;

pub use storage::Storage;

use anyhow::Result;
use hourai::{
    interactions::CommandError,
    models::{
        guild::{Guild, Permissions, Role},
        id::{GuildId, RoleId},
    },
    proto::cache::CachedRoleProto,
};
use hourai_redis::{CachedGuild, OnlineStatus, RedisPool};
use hourai_sql::{Member, SqlPool};
use rand::Rng;
use std::collections::HashSet;

pub fn is_moderator_role(role: &CachedRoleProto) -> bool {
    let name = role.get_name().to_lowercase();
    let perms = Permissions::from_bits_truncate(role.get_permissions());
    perms.contains(Permissions::ADMINISTRATOR)
        || name.starts_with("mod")
        || name.starts_with("admin")
}

pub async fn find_moderator_roles(
    guild_id: GuildId,
    redis: &mut RedisPool,
) -> Result<Vec<CachedRoleProto>> {
    Ok(CachedGuild::fetch_all_resources::<Role>(guild_id, redis)
        .await?
        .into_values()
        .filter(is_moderator_role)
        .collect())
}

pub async fn find_moderators(
    guild_id: GuildId,
    sql: &SqlPool,
    redis: &mut RedisPool,
) -> Result<Vec<Member>> {
    let mod_roles: Vec<RoleId> = find_moderator_roles(guild_id, redis)
        .await?
        .into_iter()
        .filter_map(|role| RoleId::new(role.get_role_id()))
        .collect();
    Ok(Member::find_with_roles(guild_id, mod_roles)
        .fetch_all(sql)
        .await?)
}

pub async fn find_online_moderators(
    guild_id: GuildId,
    sql: &SqlPool,
    redis: &mut RedisPool,
) -> Result<Vec<Member>> {
    let mods = find_moderators(guild_id, sql, redis).await?;
    let online =
        OnlineStatus::find_online(guild_id, mods.iter().map(|member| member.user_id()), redis)
            .await?;
    Ok(mods
        .into_iter()
        .filter(|member| online.contains(&member.user_id()))
        .collect())
}

pub async fn ping_online_mod(guild_id: GuildId, storage: &Storage) -> Result<(String, String)> {
    let mut redis = storage.redis().clone();
    let online_mods = find_online_moderators(guild_id, storage.sql(), &mut redis).await?;
    let guild = CachedGuild::fetch_resource::<Guild>(guild_id, guild_id, &mut redis)
        .await?
        .ok_or(CommandError::NotInGuild)?;

    let mention: String;
    let ping: String;
    if online_mods.is_empty() {
        mention = format!("<@{}>", guild.get_owner_id());
        ping = format!("<@{}>, No mods online!", guild.get_owner_id());
    } else {
        let idx = rand::thread_rng().gen_range(0..online_mods.len());
        mention = format!("<@{}>", online_mods[idx].user_id());
        ping = mention.clone();
    };

    Ok((mention, ping))
}

pub async fn is_moderator(
    guild_id: GuildId,
    mut roles: impl Iterator<Item = RoleId>,
    redis: &mut RedisPool,
) -> Result<bool> {
    let moderator_roles: HashSet<RoleId> = find_moderator_roles(guild_id, redis)
        .await?
        .iter()
        .filter_map(|role| RoleId::new(role.get_role_id()))
        .collect();
    Ok(roles.any(move |role_id| moderator_roles.contains(&role_id)))
}
