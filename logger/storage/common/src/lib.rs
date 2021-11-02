use anyhow::Result;
use hourai::{
    models::{
        guild::{Permissions, Role},
        id::{GuildId, RoleId},
    },
    proto::cache::CachedRoleProto,
};
use hourai_redis::{CachedGuild, OnlineStatus, RedisPool};
use hourai_sql::{Member, SqlPool};

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
