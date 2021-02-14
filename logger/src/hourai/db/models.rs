use twilight_model::id::*;
use tracing::{debug, info};

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
            let roles = self
                .role_ids
                .iter()
                .map(|id| id.to_string())
                .collect::<Vec<String>>()
                .join(", ");
            format!(
                "INSERT INTO member_roles (guild_id, user_id, role_ids)
                 VALUES ({user_id}, {guild_id}, {{{role_ids}}})
                 ON CONFLICT ON CONSTRAINT member_roles_pkey
                 DO UPDATE SET role_ids = {{{role_ids}}}",
                user_id = self.user_id, guild_id = self.guild_id, role_ids = roles
            )
        } else {
            format!(
                "DELETE FROM member_roles WHERE guild_id = {} AND user_id = {};",
                self.guild_id, self.user_id
            )
        };

        debug!("Member role query: {}", query);
        sqlx::query(query.as_ref()).execute(executor).await?;

        info!("Updated roles for member {}, guild {}", self.user_id, self.guild_id);
        return Ok(());
    }
}
