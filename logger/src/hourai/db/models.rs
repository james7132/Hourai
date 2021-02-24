use twilight_model::{
    id::*,
    user::User,
    guild::Ban as TwilightBan,
};
use std::convert::TryInto;

pub type SqlDatabase = sqlx::Postgres;
pub type SqlQuery<'a> =
    sqlx::query::Query<'a, SqlDatabase,
                       <SqlDatabase as sqlx::database::HasArguments<'a>>::Arguments>;
pub type SqlQueryAs<'a, O> =
    sqlx::query::QueryAs<'a, SqlDatabase, O,
                        <SqlDatabase as sqlx::database::HasArguments<'a>>::Arguments>;

fn get_unix_millis() -> u64 {
    std::time::SystemTime::now()
        .duration_since(std::time::SystemTime::UNIX_EPOCH)
        .expect("It's past 01/01/1970. This should be a positive value.")
        .as_millis() as u64
}

#[derive(Debug, Clone, sqlx::FromRow)]
pub struct Username {
    user_id: UserId,
    timestamp: u64,
    name: String,
    discriminator: Option<u32>,
}

impl Username {

    pub fn new(user: &User) -> Self {
        Self {
            user_id: user.id,
            timestamp: get_unix_millis(),
            name: user.name.clone(),
            discriminator: Some(user.discriminator.parse::<u32>()
                .expect("Discriminator isn't a number"))
        }
    }

    pub fn insert(&self) -> SqlQuery {
        sqlx::query("INSERT INTO usernames (user_id, timestamp, name, discriminator)
                     VALUES ($1, $2, $3, $4)
                     ON CONFLICT ON CONSTRAINT idx_unique_username
                     DO NOTHING")
             .bind(self.user_id.0 as i64)
             .bind(self.timestamp as i64)
             .bind(self.name.clone())
             .bind(self.discriminator)
    }

}

#[derive(Debug, sqlx::FromRow)]
pub struct Ban {
    pub guild_id: i64,
    pub user_id: i64,
    pub reason: Option<String>,
    pub avatar: Option<String>
}

impl Ban {

    pub fn from(guild_id: GuildId, ban: TwilightBan) -> Self {
        Self {
            guild_id: guild_id.0 as i64,
            user_id: ban.user.id.0 as i64,
            reason: ban.reason,
            avatar: ban.user.avatar
        }
    }

    pub fn insert<'a>(self) -> SqlQuery<'a> {
        sqlx::query("INSERT INTO bans (guild_id, user_id, reason, avatar)
                     VALUES ($1, $2, $3, $4)
                     ON CONFLICT ON CONSTRAINT members_pkey
                     DO UPDATE SET reason = excluded.reason, avatar = excluded.avatar")
            .bind(self.user_id)
            .bind(self.guild_id)
            .bind(self.reason)
            .bind(self.avatar)
    }

    pub fn clear_ban<'a>(guild_id: GuildId, user_id: UserId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM bans WHERE guild_id = $1 AND user_id = $2")
            .bind(guild_id.0 as i64)
            .bind(user_id.0 as i64)
    }

    pub fn clear_guild<'a>(guild_id: GuildId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM bans WHERE guild_id = $1")
            .bind(guild_id.0 as i64)
    }

    pub fn get_guild_bans<'a>(guild_id: GuildId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT guild_id, user_id, reason, avatar FROM bans WHERE guild_id = $1")
            .bind(guild_id.0 as i64)
    }

    pub fn get_user_bans<'a>(user_id: UserId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT guild_id, user_id, reason, avatar FROM bans WHERE user_id = $1")
            .bind(user_id.0 as i64)
    }

}

#[derive(Debug, sqlx::FromRow)]
pub struct Member {
    pub guild_id: i64,
    pub user_id: i64,
    pub role_ids: Vec<i64>,
    pub nickname: Option<String>,
}

impl Member {

    pub fn from(member: &twilight_model::guild::member::Member) -> Self {
        Self {
            guild_id: member.guild_id.0 as i64,
            user_id: member.user.id.0 as i64,
            role_ids: member.roles.iter().map(|id| id.0 as i64).collect(),
            nickname: member.nick.clone()
        }
    }

    pub fn guild_id(&self) -> GuildId {
        GuildId(self.user_id as u64)
    }

    pub fn user_id(&self) -> UserId {
        UserId(self.user_id as u64)
    }

    pub fn role_ids<'a>(&'a self) ->  impl Iterator<Item=RoleId> + 'a {
        self.role_ids.iter().map(|id| RoleId(id.clone().try_into().unwrap()))
    }

    pub fn insert<'a>(self) -> SqlQuery<'a> {
        sqlx::query("INSERT INTO members (guild_id, user_id, role_ids, nickname)
                     VALUES ($1, $2, $3, $4)
                     ON CONFLICT ON CONSTRAINT members_pkey
                     DO UPDATE SET role_ids = $3, nickname = $4")
            .bind(self.user_id)
            .bind(self.guild_id)
            .bind(self.role_ids)
            .bind(self.nickname)
    }

    pub fn fetch<'a>(guild_id: GuildId, user_id: UserId) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT FROM members WHERE guild_id = $1 AND user_id = $2")
             .bind(guild_id.0 as i64)
             .bind(user_id.0 as i64)
    }

    /// Clears all of the records for a server
    pub fn clear_guild<'a>(guild_id: GuildId) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM members WHERE guild_id = $1")
             .bind(guild_id.0 as i64)
    }

    /// Deletes all record of a single role from the database
    pub fn clear_role<'a>(guild_id: GuildId, role_id: RoleId) -> SqlQuery<'a> {
        sqlx::query("UPDATE members
                     SET role_ids = array_remove(role_ids, $1)
                     WHERE guild_id = $2")
             .bind(role_id.0 as i64)
             .bind(guild_id.0 as i64)
    }
}
