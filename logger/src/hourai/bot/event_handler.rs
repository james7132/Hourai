use crate::hourai::db::MemberRoles;
use twilight_gateway::Event;
use twilight_model::gateway::payload::*;
use tracing::{debug, error};

#[derive(Clone)]
pub struct EventHandler {
    pub sql: sqlx::PgPool,
}

impl EventHandler {
    pub fn consume_event(&self, shard_id: u64, event: twilight_gateway::Event) {
        match event {
            Event::MessageCreate(_) =>
                debug!("Shard {} got a MESSAGE_CREATE event.", shard_id),
            Event::MemberUpdate(evt) => {
                debug!("Shard {} got a GUILD_MEMBER_UPDATE event.", shard_id);
                tokio::spawn(self.clone().member_update(*evt));
            },
            _ => panic!("Unexpected event type: {:?}", event),
        }
    }

    async fn member_update(self, evt: MemberUpdate) {
        if let Err(err) = MemberRoles::new(evt.guild_id, evt.user.id, &evt.roles)
            .log(&self.sql)
            .await
        {
            error!(
                "Failed to log roles for member {}, guild {}: {:?}",
                evt.user.id, evt.guild_id, err
            );
        }
    }
}
