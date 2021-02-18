use twilight_model::id::*;
use sqlx::prelude::*;
use tracing::info;

#[derive(Debug, Clone)]
pub struct UnixTimestamp {
    timestamp: std::time::Instant,
}

#[derive(Debug, Clone, sqlx::FromRow)]
pub struct Username {
    user_id: UserId,
    timestamp: UnixTimestamp,
    name: String,
    discriminator: u16,
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

    /// Logs a set of member role IDs
    pub async fn log(&self, executor: &sqlx::PgPool) -> sqlx::Result<()> {
        let query = if self.role_ids.len() != 0 {
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
        };

        query.execute(executor).await?;

        info!("Updated roles for member {}, guild {}", self.user_id, self.guild_id);
        return Ok(());
    }
}
