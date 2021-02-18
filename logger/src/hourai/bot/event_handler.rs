use crate::hourai::db::MemberRoles;
use twilight_gateway::{Event, EventTypeFlags};
use twilight_model::gateway::payload::*;
use tracing::error;

#[derive(Clone)]
pub struct EventHandler {
    pub sql: sqlx::PgPool,
}

impl EventHandler {
    pub const BOT_EVENTS : EventTypeFlags =
        EventTypeFlags::from_bits_truncate(
            EventTypeFlags::MEMBER_UPDATE.bits() |
            EventTypeFlags::GUILD_DELETE.bits() |
            EventTypeFlags::ROLE_DELETE.bits());

    pub fn consume_event(&self, event: twilight_gateway::Event) {
        match event {
            Event::MemberUpdate(evt) => { tokio::spawn(self.clone().on_member_update(*evt)); },
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    tokio::spawn(self.clone().on_guild_leave(*evt));
                }
            },
            Event::RoleDelete(evt) => { tokio::spawn(self.clone().on_role_delete(evt)); },
            _ => panic!("Unexpected event type: {:?}", event),
        };
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> () {
        if let Err(err) = MemberRoles::clear_guild(evt.id, &self.sql).await {
            error!("Failed to clear roles for guild {}: {:?}", evt.id, err);
        }
    }

    async fn on_role_delete(self, evt: RoleDelete) -> () {
        if let Err(err) = MemberRoles::clear_role(evt.guild_id, evt.role_id, &self.sql).await {
            error!("Failed to clear role {} for guild {}: {:?}", evt.role_id, evt.guild_id, err);
        }
    }

    async fn on_member_update(self, evt: MemberUpdate) -> () {
        if evt.user.bot {
            return;
        }
        if let Err(err) = MemberRoles::new(evt.guild_id, evt.user.id, &evt.roles)
            .log(&self.sql)
            .await
        {
            error!("Failed to log roles for member {}, guild {}: {:?}",
                   evt.user.id, evt.guild_id, err);
        }
    }
}
