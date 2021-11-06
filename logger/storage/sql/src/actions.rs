use crate::{
    models::{Member, SqlQuery, SqlQueryAs},
    types, SqlPool,
};
use anyhow::Result;
use chrono::Duration;
use hourai::{
    http::{self, request::AuditLogReason},
    models::id::*,
    proto::action::*,
};
use sqlx::types::chrono::{DateTime, Utc};
use std::{collections::HashSet, sync::Arc};

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
        sqlx::query_as("SELECT id, data FROM pending_actions WHERE ts < now()")
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

#[derive(Clone)]
pub struct ActionExecutor {
    http: Arc<http::Client>,
    sql: SqlPool,
}

impl ActionExecutor {
    pub fn new(http: Arc<http::Client>, sql: SqlPool) -> Self {
        Self { http, sql }
    }

    pub fn http(&self) -> &Arc<http::Client> {
        &self.http
    }

    pub fn sql(&self) -> &SqlPool {
        &self.sql
    }

    pub async fn execute_action(&self, action: &Action) -> Result<()> {
        match action.details {
            Some(Action_oneof_details::kick(_)) => self.execute_kick(action).await?,
            Some(Action_oneof_details::ban(ref info)) => self.execute_ban(action, &info).await?,
            Some(Action_oneof_details::escalate(ref info)) => {
                self.execute_escalate(action, &info).await?
            }
            Some(Action_oneof_details::mute(ref info)) => self.execute_mute(action, &info).await?,
            Some(Action_oneof_details::deafen(ref info)) => {
                self.execute_deafen(action, &info).await?
            }
            Some(Action_oneof_details::change_role(ref info)) => {
                self.execute_change_role(action, &info).await?
            }
            Some(Action_oneof_details::direct_message(ref info)) => {
                self.execute_direct_message(action, &info).await?
            }
            Some(Action_oneof_details::send_message(ref info)) => {
                self.execute_send_message(&info).await?
            }
            None => panic!("Cannot run action without a specified type"),
        };

        // Schedule undo if a duration is set
        if action.has_duration() {
            let timestamp = Utc::now() + Duration::seconds(action.get_duration() as i64);
            let mut undo = action.clone();
            Self::invert_action(&mut undo);
            undo.clear_duration();
            PendingAction::schedule(undo, timestamp)
                .execute(&self.sql)
                .await?;
        }
        Ok(())
    }

    fn invert_action(action: &mut Action) {
        match &mut action.details {
            Some(Action_oneof_details::ban(ref mut info)) => {
                info.set_field_type(match info.get_field_type() {
                    BanMember_Type::BAN => BanMember_Type::UNBAN,
                    BanMember_Type::UNBAN => BanMember_Type::BAN,
                    BanMember_Type::SOFTBAN => panic!("Cannot invert a softban"),
                });
            }
            Some(Action_oneof_details::escalate(ref mut info)) => {
                info.set_amount(-info.get_amount());
            }
            Some(Action_oneof_details::mute(ref mut info)) => {
                info.set_field_type(Self::invert_status(info.get_field_type()));
            }
            Some(Action_oneof_details::deafen(ref mut info)) => {
                info.set_field_type(Self::invert_status(info.get_field_type()));
            }
            Some(Action_oneof_details::change_role(ref mut info)) => {
                info.set_field_type(Self::invert_status(info.get_field_type()));
            }
            Some(_) => {
                panic!("Cannot invert action: {:?}", action);
            }
            None => panic!("Cannot invert action without a specified type"),
        }
    }

    fn invert_status(status: StatusType) -> StatusType {
        match status {
            StatusType::APPLY => StatusType::UNAPPLY,
            StatusType::UNAPPLY => StatusType::APPLY,
            StatusType::TOGGLE => StatusType::TOGGLE,
        }
    }

    async fn execute_kick(&self, action: &Action) -> Result<()> {
        let guild_id = GuildId::new(action.get_guild_id()).unwrap();
        let user_id = UserId::new(action.get_user_id()).unwrap();
        self.http
            .remove_guild_member(guild_id, user_id)
            .reason(action.get_reason())?
            .exec()
            .await?;
        Ok(())
    }

    async fn execute_ban(&self, action: &Action, info: &BanMember) -> Result<()> {
        let guild_id = GuildId::new(action.get_guild_id()).unwrap();
        let user_id = UserId::new(action.get_user_id()).unwrap();
        if info.get_field_type() != BanMember_Type::UNBAN {
            self.http
                .create_ban(guild_id, user_id)
                .reason(action.get_reason())?
                .delete_message_days(info.get_delete_message_days() as u64)?
                .exec()
                .await?;
        }
        if info.get_field_type() != BanMember_Type::BAN {
            self.http
                .delete_ban(guild_id, user_id)
                .reason(action.get_reason())?
                .exec()
                .await?;
        }
        Ok(())
    }

    async fn execute_escalate(&self, action: &Action, info: &EscalateMember) -> Result<()> {
        // TODO(james7132): Implement
        Ok(())
    }

    async fn execute_mute(&self, action: &Action, info: &MuteMember) -> Result<()> {
        let guild_id = GuildId::new(action.get_guild_id()).unwrap();
        let user_id = UserId::new(action.get_user_id()).unwrap();
        let mute = match info.get_field_type() {
            StatusType::APPLY => true,
            StatusType::UNAPPLY => false,
            StatusType::TOGGLE => {
                !self
                    .http
                    .guild_member(guild_id, user_id)
                    .exec()
                    .await?
                    .model()
                    .await?
                    .mute
            }
        };

        self.http
            .update_guild_member(guild_id, user_id)
            .mute(mute)
            .reason(action.get_reason())?
            .exec()
            .await?;

        Ok(())
    }

    async fn execute_deafen(&self, action: &Action, info: &DeafenMember) -> Result<()> {
        let guild_id = GuildId::new(action.get_guild_id()).unwrap();
        let user_id = UserId::new(action.get_user_id()).unwrap();
        let deafen = match info.get_field_type() {
            StatusType::APPLY => true,
            StatusType::UNAPPLY => false,
            StatusType::TOGGLE => {
                !self
                    .http
                    .guild_member(guild_id, user_id)
                    .exec()
                    .await?
                    .model()
                    .await?
                    .deaf
            }
        };

        self.http
            .update_guild_member(guild_id, user_id)
            .deaf(deafen)
            .reason(action.get_reason())?
            .exec()
            .await?;

        Ok(())
    }

    async fn execute_change_role(&self, action: &Action, info: &ChangeRole) -> Result<()> {
        let guild_id = GuildId::new(action.get_guild_id()).unwrap();
        let user_id = UserId::new(action.get_user_id()).unwrap();
        let member = Member::fetch(guild_id, user_id)
            .fetch_one(&self.sql)
            .await?;

        if !member.present && info.get_role_ids().is_empty() {
            return Ok(());
        }

        let role_ids: HashSet<RoleId> = info
            .get_role_ids()
            .iter()
            .cloned()
            .filter_map(RoleId::new)
            .collect();
        let mut roles: HashSet<RoleId> = member.role_ids().collect();
        match info.get_field_type() {
            StatusType::APPLY => {
                roles.extend(role_ids);
            }
            StatusType::UNAPPLY => {
                roles.retain(|role_id| !role_ids.contains(&role_id));
            }
            StatusType::TOGGLE => {
                for role_id in role_ids {
                    if roles.contains(&role_id) {
                        roles.remove(&role_id);
                    } else {
                        roles.insert(role_id);
                    }
                }
            }
        };

        let roles: Vec<RoleId> = roles.into_iter().collect();
        self.http
            .update_guild_member(guild_id, user_id)
            .roles(&roles)
            .reason(action.get_reason())?
            .exec()
            .await?;
        Ok(())
    }

    async fn execute_direct_message(&self, action: &Action, info: &DirectMessage) -> Result<()> {
        let user_id = UserId::new(action.get_user_id()).unwrap();
        let channel = self
            .http
            .create_private_channel(user_id)
            .exec()
            .await?
            .model()
            .await?;

        self.http
            .create_message(channel.id)
            .content(info.get_content())?
            .exec()
            .await?;
        Ok(())
    }

    async fn execute_send_message(&self, info: &SendMessage) -> Result<()> {
        let channel_id = ChannelId::new(info.get_channel_id()).unwrap();
        self.http
            .create_message(channel_id)
            .content(info.get_content())?
            .exec()
            .await?;
        Ok(())
    }
}
