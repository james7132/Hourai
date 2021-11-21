use crate::types;
use hourai::{
    models::{
        datetime::Timestamp, gateway::payload::incoming::MemberUpdate, guild::Ban as TwilightBan,
        guild::Member as TwilightMember, id::*, UserLike,
    },
    proto::action::{Action, ActionSet},
};
use sqlx::types::chrono::{DateTime, NaiveDateTime, Utc};
use std::convert::TryInto;

pub type SqlDatabase = sqlx::Postgres;
pub type SqlQuery<'a> = sqlx::query::Query<
    'a,
    SqlDatabase,
    <SqlDatabase as sqlx::database::HasArguments<'a>>::Arguments,
>;
pub type SqlQueryAs<'a, O> = sqlx::query::QueryAs<
    'a,
    SqlDatabase,
    O,
    <SqlDatabase as sqlx::database::HasArguments<'a>>::Arguments,
>;

fn to_datetime(timestamp: &Timestamp) -> DateTime<Utc> {
    let micros = timestamp.as_micros();
    let secs = micros / 1000000;
    let nsecs = (micros % 1000000) * 1000;
    DateTime::from_utc(
        NaiveDateTime::from_timestamp(secs as i64, nsecs as u32),
        Utc,
    )
}

#[derive(Debug, Clone, sqlx::FromRow)]
pub struct Username {
    pub user_id: i64,
    pub timestamp: DateTime<Utc>,
    pub name: String,
    pub discriminator: Option<i32>,
}

impl Username {
    pub fn new(user: &impl UserLike) -> Self {
        Self {
            user_id: user.id().get() as i64,
            timestamp: Utc::now(),
            name: user.name().to_owned(),
            discriminator: Some(user.discriminator() as i32),
        }
    }

    pub fn fetch<'a>(user_id: UserId, limit: Option<u64>) -> SqlQueryAs<'a, Self> {
        if let Some(max) = limit {
            sqlx::query_as(
                "SELECT user_id, timestamp, name, discriminator \
                 FROM usernames WHERE user_id = $1 LIMIT $2",
            )
            .bind(user_id.get() as i64)
            .bind(max as i64)
        } else {
            sqlx::query_as(
                "SELECT user_id, timestamp, name, discriminator \
                 FROM usernames WHERE user_id = $1",
            )
            .bind(user_id.get() as i64)
        }
    }

    pub fn insert(&self) -> SqlQuery {
        sqlx::query(
            "INSERT INTO usernames (user_id, name, discriminator) \
             VALUES ($1, $2, $3) \
             ON CONFLICT ON CONSTRAINT idx_unique_username \
             DO NOTHING",
        )
        .bind(self.user_id)
        .bind(self.name.clone())
        .bind(self.discriminator)
    }

    pub fn bulk_insert<'a>(usernames: Vec<Self>) -> SqlQuery<'a> {
        let user_ids: Vec<i64> = usernames.iter().map(|u| u.user_id).collect();
        let names: Vec<String> = usernames.iter().map(|u| u.name.clone()).collect();
        let discriminator: Vec<Option<i32>> = usernames.iter().map(|u| u.discriminator).collect();
        sqlx::query(
            "INSERT INTO usernames (user_id, name, discriminator) \
             SELECT * FROM UNNEST ($1, $2, $3) \
             AS t(user_id, name, discriminator) \
             ON CONFLICT ON CONSTRAINT idx_unique_username \
             DO NOTHING",
        )
        .bind(user_ids)
        .bind(names)
        .bind(discriminator)
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct VerificationBan {
    pub user_id: i64,
    pub reason: Option<String>,
    pub name: String,
    pub discriminator: String,
}

impl VerificationBan {
    pub fn fetch_by_name<'a>(guild_id: GuildId, name: impl Into<String>) -> SqlQueryAs<'a, Self> {
        let mut name = name.into();
        name.make_ascii_lowercase();
        sqlx::query_as(
            "SELECT \
                ban.user_id, ban.reason, username.name, username.discriminator \
            FROM bans \
            LEFT JOIN usernames \
                ON bans.user_id = usernames.user_id \
            WHERE \
                ban.guild_id = $1 AND \
                LOWER(username.name) = $2",
        )
        .bind(guild_id.get() as i64)
        .bind(name)
    }

    pub fn fetch_by_avatar<'a>(
        guild_id: GuildId,
        avatar: impl Into<String>,
    ) -> SqlQueryAs<'a, Self> {
        let mut avatar = avatar.into();
        avatar.make_ascii_lowercase();
        sqlx::query_as(
            "SELECT \
                ban.user_id, ban.reason, username.name, username.discriminator \
            FROM bans \
            LEFT JOIN usernames \
                ON bans.user_id = usernames.user_id \
            WHERE \
                ban.guild_id = $1 AND \
                LOWER(ban.avatar) = $2",
        )
        .bind(guild_id.get() as i64)
        .bind(avatar)
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct Ban {
    pub guild_id: i64,
    pub user_id: i64,
    pub reason: Option<String>,
    pub avatar: Option<String>,
}

impl Ban {
    pub fn from(guild_id: GuildId, ban: TwilightBan) -> Self {
        Self {
            guild_id: guild_id.get() as i64,
            user_id: ban.user.id.get() as i64,
            reason: ban.reason,
            avatar: ban.user.avatar,
        }
    }

    pub fn guild_id(&self) -> GuildId {
        unsafe { GuildId::new_unchecked(self.guild_id as u64) }
    }

    /// Constructs a query to add a single ban.
    pub fn insert<'a>(self) -> SqlQuery<'a> {
        sqlx::query(
            "INSERT INTO bans (guild_id, user_id, reason, avatar) \
                     VALUES ($1, $2, $3, $4) \
                     ON CONFLICT ON CONSTRAINT bans_pkey \
                     DO UPDATE SET reason = excluded.reason, avatar = excluded.avatar",
        )
        .bind(self.guild_id)
        .bind(self.user_id)
        .bind(self.reason)
        .bind(self.avatar)
    }

    /// Constructs a query to bulk add multiple bans.
    pub fn bulk_insert<'a>(bans: Vec<Self>) -> SqlQuery<'a> {
        let guild_ids: Vec<i64> = bans.iter().map(|b| b.guild_id).collect();
        let user_ids: Vec<i64> = bans.iter().map(|b| b.user_id).collect();
        let reasons: Vec<Option<String>> = bans.iter().map(|b| b.reason.clone()).collect();
        let avatars: Vec<Option<String>> = bans.iter().map(|b| b.avatar.clone()).collect();
        sqlx::query(
            "INSERT INTO bans (guild_id, user_id, reason, avatar) \
                     SELECT * FROM UNNEST ($1, $2, $3, $4) \
                     AS t(guild_id, user_id, reason, avatar) \
                     ON CONFLICT ON CONSTRAINT bans_pkey \
                     DO UPDATE SET reason = excluded.reason, avatar = excluded.avatar",
        )
        .bind(guild_ids)
        .bind(user_ids)
        .bind(reasons)
        .bind(avatars)
    }

    /// Constructs a query to clear a single user's ban from a given guild.
    pub fn clear_ban<'a>(guild_id: GuildId, user_id: UserId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM bans WHERE guild_id = $1 AND user_id = $2")
            .bind(guild_id.get() as i64)
            .bind(user_id.get() as i64)
    }

    /// Constructs a query to clear a all bans from a given guild.
    pub fn clear_guild<'a>(guild_id: GuildId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM bans WHERE guild_id = $1").bind(guild_id.get() as i64)
    }

    /// Constructs a query to clear a all bans from a given shard.
    pub fn clear_shard<'a>(shard_id: u64, shard_total: u64) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM bans WHERE (guild_id >> 22) % $2 = $1")
            .bind(shard_id as i64)
            .bind(shard_total as i64)
    }

    /// Constructs a query to retreive all bans from a given guild.
    pub fn fetch_guild_bans<'a>(guild_id: GuildId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT guild_id, user_id, reason, avatar FROM bans WHERE guild_id = $1")
            .bind(guild_id.get() as i64)
    }

    /// Constructs a query to retreive all bans for a given user, ignoring certain servers.
    pub fn fetch_user_bans<'a>(user_id: UserId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as(
            "SELECT \
                bans.guild_id, bans.user_id, bans.reason, bans.avatar \
            FROM \
                bans \
            FULL OUTER JOIN \
                admin_configs \
            WHERE \
                bans.user_id = $1 AND \
                (admin_configs.id IS NULL OR admin_configs.source_bans = true)",
        )
        .bind(user_id.get() as i64)
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct Member {
    pub guild_id: i64,
    pub user_id: i64,
    pub role_ids: Vec<i64>,
    pub nickname: Option<String>,
    pub bot: bool,
    pub present: bool,
    pub premium_since: Option<DateTime<Utc>>,
    pub avatar: Option<String>,
}

impl From<&TwilightMember> for Member {
    fn from(member: &TwilightMember) -> Self {
        let premium = member.premium_since.as_ref().map(|p| to_datetime(p));
        Self {
            guild_id: member.guild_id.get() as i64,
            user_id: member.user.id.get() as i64,
            role_ids: member.roles.iter().map(|id| id.get() as i64).collect(),
            nickname: member.nick.clone(),
            bot: member.user.bot,
            present: true,
            premium_since: premium,
            avatar: member.avatar.clone(),
        }
    }
}

impl From<&MemberUpdate> for Member {
    fn from(member: &MemberUpdate) -> Self {
        let premium = member.premium_since.as_ref().map(|p| to_datetime(p));
        Self {
            guild_id: member.guild_id.get() as i64,
            user_id: member.user.id.get() as i64,
            role_ids: member.roles.iter().map(|id| id.get() as i64).collect(),
            nickname: member.nick.clone(),
            bot: member.user.bot,
            present: true,
            premium_since: premium,
            avatar: member.avatar.clone(),
        }
    }
}

impl Member {
    pub fn guild_id(&self) -> GuildId {
        unsafe { GuildId::new_unchecked(self.user_id as u64) }
    }

    pub fn user_id(&self) -> UserId {
        unsafe { UserId::new_unchecked(self.user_id as u64) }
    }

    pub fn role_ids(&self) -> impl Iterator<Item = RoleId> + '_ {
        unsafe {
            self.role_ids
                .iter()
                .map(|id| RoleId::new_unchecked(id.clone().try_into().unwrap()))
        }
    }

    pub fn set_present<'a>(guild_id: GuildId, user_id: UserId, present: bool) -> SqlQuery<'a> {
        sqlx::query(
            "UPDATE members SET present = $1, last_seen = now() \
                     WHERE guild_id = $2 AND user_id = $3",
        )
        .bind(present)
        .bind(guild_id.get() as i64)
        .bind(user_id.get() as i64)
    }

    pub fn insert<'a>(self) -> SqlQuery<'a> {
        sqlx::query(
            "INSERT INTO members (
                guild_id,
                user_id,
                role_ids,
                nickname,
                present,
                bot,
                premium_since
                avatar
            ) \
            VALUES ($1, $2, $3, $4, true, $5, $6, $7) \
            ON CONFLICT ON CONSTRAINT members_pkey \
            DO UPDATE SET \
                role_ids = excluded.role_ids, \
                nickname = excluded.nickname, \
                premium_since = excluded.premium_since, \
                avatar = excluded.avatar, \
                bot = excluded.bot, \
                last_seen = now(), \
                present = true",
        )
        .bind(self.guild_id)
        .bind(self.user_id)
        .bind(self.role_ids)
        .bind(self.nickname)
        .bind(self.bot)
        .bind(self.premium_since)
        .bind(self.avatar)
    }

    pub fn has_nitro<'a>(user_id: UserId) -> SqlQueryAs<'a, (bool,)> {
        sqlx::query_as("SELECT EXISTS( \
                SELECT 1 FROM members \
                WHERE \
                    user_id = $1 AND \
                    present AND \
                    (premium_since IS NOT NULL OR avatar IS NOT NULL)
            )")
            .bind(user_id.get() as i64)
    }

    pub fn count_guilds<'a>() -> SqlQueryAs<'a, (i64,)> {
        sqlx::query_as("SELECT count(distinct guild_id) FROM members")
    }

    pub fn count_members<'a>() -> SqlQueryAs<'a, (i64,)> {
        sqlx::query_as("SELECT count(*) FROM members")
    }

    pub fn count_guild_members<'a>(
        guild_id: GuildId,
        include_bots: bool,
    ) -> SqlQueryAs<'a, (i64,)> {
        if include_bots {
            sqlx::query_as("SELECT count(*) FROM members WHERE guild_id = $1 AND present")
                .bind(guild_id.get() as i64)
        } else {
            sqlx::query_as(
                "SELECT count(*) FROM members WHERE guild_id = $1 AND present AND NOT bot",
            )
            .bind(guild_id.get() as i64)
        }
    }

    pub fn fetch<'a>(guild_id: GuildId, user_id: UserId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT * FROM members WHERE guild_id = $1 AND user_id = $2")
            .bind(guild_id.get() as i64)
            .bind(user_id.get() as i64)
    }

    pub fn find_with_roles<'a>(
        guild_id: GuildId,
        role_ids: impl IntoIterator<Item = RoleId>,
    ) -> SqlQueryAs<'a, Self> {
        let role_ids: Vec<i64> = role_ids.into_iter().map(|id| id.get() as i64).collect();
        sqlx::query_as("SELECT * FROM members WHERE present AND guild_id = $1 AND role_ids && $2")
            .bind(guild_id.get() as i64)
            .bind(role_ids)
    }

    /// Marks all members as not present in preparation for repopulating the column.
    pub fn clear_present_shard<'a>(shard_id: u64, shard_total: u64) -> SqlQuery<'a> {
        sqlx::query("UPDATE members SET present = false WHERE (guild_id >> 22) % $2 = $1")
            .bind(shard_id as i64)
            .bind(shard_total as i64)
    }

    /// Clears all of the records for a server
    pub fn clear_guild<'a>(guild_id: GuildId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM members WHERE guild_id = $1").bind(guild_id.get() as i64)
    }

    /// Deletes all record of a single role from the database
    pub fn clear_role<'a>(guild_id: GuildId, role_id: RoleId) -> SqlQuery<'a> {
        sqlx::query(
            "UPDATE members \
             SET role_ids = array_remove(role_ids, $1) \
             WHERE guild_id = $2",
        )
        .bind(role_id.get() as i64)
        .bind(guild_id.get() as i64)
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct EscalationEntry {
    pub guild_id: i64,
    pub subject_id: i64,
    pub authorizer_id: i64,
    pub authorizer_name: String,
    pub display_name: String,
    pub timestamp: DateTime<Utc>,
    pub action: types::Protobuf<ActionSet>,
    pub level_delta: i32,
}

impl EscalationEntry {
    pub fn guild_id(&self) -> GuildId {
        unsafe { GuildId::new_unchecked(self.guild_id as u64) }
    }

    pub fn subject_id(&self) -> UserId {
        unsafe { UserId::new_unchecked(self.subject_id as u64) }
    }

    pub fn authorizer_id(&self) -> UserId {
        unsafe { UserId::new_unchecked(self.authorizer_id as u64) }
    }

    pub fn fetch<'a>(guild_id: GuildId, user_id: UserId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as(
            "SELECT * FROM escalation_histories \
             WHERE guild_id = $1 AND subject_id = $2 \
             ORDER BY timestamp",
        )
        .bind(guild_id.get() as i64)
        .bind(user_id.get() as i64)
    }

    pub fn insert<'a>(&self) -> SqlQueryAs<'a, (i32,)> {
        sqlx::query_as(
            "INSERT INTO escalation_histories ( \
                guild_id, \
                subject_id, \
                authorizer_id, \
                authorizer_name, \
                display_name, \
                action, \
                level_delta, \
                timestamp \
            ) \
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8) \
            RETURNING id",
        )
        .bind(self.guild_id)
        .bind(self.subject_id)
        .bind(self.authorizer_id)
        .bind(self.authorizer_name.clone())
        .bind(self.display_name.clone())
        .bind(self.action.clone())
        .bind(self.level_delta)
        .bind(self.timestamp)
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct PendingDeescalation {
    pub guild_id: i64,
    pub user_id: i64,
    pub expiration: DateTime<Utc>,
    pub amount: i64,
    pub entry_id: i32,
}

impl PendingDeescalation {
    #[inline(always)]
    pub fn guild_id(&self) -> GuildId {
        unsafe { GuildId::new_unchecked(self.guild_id as u64) }
    }

    #[inline(always)]
    pub fn user_id(&self) -> UserId {
        unsafe { UserId::new_unchecked(self.user_id as u64) }
    }

    pub fn insert(&self) -> SqlQuery {
        sqlx::query(
            "INSERT INTO pending_deescalations ( \
                guild_id, \
                user_id, \
                expiration, \
                amount, \
                entry_id \
            ) \
            VALUES ($1, $2, $3, $4, $5) \
            ON CONFLICT ON CONSTRAINT pending_deescalations_pkey \
            DO UPDATE SET \
                amount = excluded.amount, \
                expiration = excluded.expiration, \
                entry_id = excluded.entry_id \
            ",
        )
        .bind(self.guild_id)
        .bind(self.user_id)
        .bind(self.expiration)
        .bind(self.amount)
        .bind(self.entry_id)
    }

    pub fn delete<'a>(guild_id: GuildId, user_id: UserId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM pending_deescalations WHERE guild_id = $1 AND user_id = $2")
            .bind(guild_id.get() as i64)
            .bind(user_id.get() as i64)
    }

    pub fn fetch_expired<'a>() -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT * FROM pending_deescalations WHERE expiration < now()")
    }
}

#[derive(Debug, sqlx::FromRow)]
pub struct PendingAction {
    id: i32,
    data: types::Protobuf<Action>,
}

impl PendingAction {
    pub fn action(&self) -> &Action {
        &self.data.0
    }

    pub fn fetch_expired<'a>() -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT id, data FROM pending_actions WHERE timestamp < now()")
    }

    pub fn schedule<'a>(action: Action, timestamp: impl Into<DateTime<Utc>>) -> SqlQuery<'a> {
        sqlx::query("INSERT INTO pending_actions (timestamp, data) VALUES ($1, $2)")
            .bind(timestamp.into())
            .bind(types::Protobuf(action))
    }

    pub fn delete<'a>(&self) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM pending_actions WHERE id = $1").bind(self.id)
    }
}
