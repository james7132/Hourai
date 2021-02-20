use twilight_model::id::*;
use twilight_model::user::User;
use tracing::{info, error};

pub type SqlDatabase = sqlx::Postgres;
pub type SqlQuery<'a> =
    sqlx::query::Query<'a, SqlDatabase,
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

    pub async fn log<'a>(&self, executor: impl sqlx::Executor<'a, Database=SqlDatabase>) -> () {
        if let Err(err) = self.insert().execute(executor).await {
            error!("Failed to log username for {}: {:?}", self.user_id, err);
        } else {
            info!("Logged username for user {}", self.user_id);
        }
    }
}

#[derive(Debug, Clone, sqlx::FromRow)]
pub struct MemberRoles {
    guild_id: GuildId,
    user_id: UserId,
    role_ids: Vec<RoleId>,
}

impl MemberRoles {

    pub fn new(guild_id: GuildId, user_id: UserId, role_ids: &Vec<RoleId>) -> MemberRoles {
        return MemberRoles {
            guild_id: guild_id,
            user_id: user_id,
            role_ids: role_ids.to_vec()
        };
    }

    pub fn insert(&self) -> SqlQuery {
        if self.role_ids.len() != 0 {
            let roles: Vec<i64> = self
                .role_ids
                .iter()
                .map(|id| id.0 as i64)
                .collect();
            sqlx::query("INSERT INTO member_roles (guild_id, user_id, role_ids)
                         VALUES ($1, $2, $3)
                         ON CONFLICT ON CONSTRAINT member_roles_pkey
                         DO UPDATE SET role_ids = $3")
                .bind(self.user_id.0 as i64)
                .bind(self.guild_id.0 as i64)
                .bind(roles)
        } else {
            sqlx::query("DELETE FROM member_roles WHERE guild_id = $1 AND user_id = $2")
                .bind(self.user_id.0 as i64)
                .bind(self.guild_id.0 as i64)
        }
    }

    /// Logs a set of member role IDs
    pub async fn log(&self, executor: &sqlx::PgPool) -> () {
        if let Err(err) = self.insert().execute(executor).await {
            error!("Failed to log roles for member {}, guild {}: {:?}",
                   self.user_id, self.guild_id, err);
        } else {
            info!("Updated roles for member {}, guild {}", self.user_id, self.guild_id);
        }
    }

    /// Clears all of the records for a server
    pub async fn clear_guild(guild_id: GuildId, executor: &sqlx::PgPool) -> () {
        let result = sqlx::query("DELETE FROM member_roles WHERE guild_id = $1")
                          .bind(guild_id.0 as i64)
                          .execute(executor)
                          .await;
        if let Err(err) = result {
            error!("Failed to clear roles for guild {}: {:?}", guild_id, err);
        } else {
            info!("Cleared stored roles for guild {}", guild_id);
        }
    }

    /// Deletes all record of a single role from the database
    pub async fn clear_role(guild_id: GuildId, role_id: RoleId, executor: &sqlx::PgPool) -> () {
        let result = sqlx::query("UPDATE member_roles
                                  SET role_ids = array_remove(role_ids, $1)
                                  WHERE guild_id = $2")
                          .bind(role_id.0 as i64)
                          .bind(guild_id.0 as i64)
                          .execute(executor)
                          .await;
        if let Err(err) = result {
            error!("Failed to clear role {} for guild {}: {:?}", role_id, guild_id, err);
        } else {
            info!("Cleared role {} from guild {}", role_id, guild_id);
        }
    }
}
