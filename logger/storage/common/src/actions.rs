use crate::{escalation::EscalationManager, Storage};
use anyhow::Result;
use chrono::{Duration, Utc};
use futures::future::{BoxFuture, FutureExt};
use hourai::{
    http::{self, request::AuditLogReason},
    models::{id::*, user::User},
    proto::action::*,
};
use hourai_sql::{Member, PendingAction};
use std::{collections::HashSet, sync::Arc};

#[derive(Clone)]
pub struct ActionExecutor {
    current_user: User,
    http: Arc<http::Client>,
    storage: Storage,
}

impl ActionExecutor {
    pub fn new(current_user: User, http: Arc<http::Client>, storage: Storage) -> Self {
        Self {
            current_user,
            http,
            storage,
        }
    }

    #[inline(always)]
    pub fn current_user(&self) -> &User {
        &self.current_user
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<http::Client> {
        &self.http
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        &self.storage
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
                if let Err(err) = self.execute_direct_message(action, &info).await {
                    tracing::error!(
                        "Error while sending a message to a given channel for an action: {}",
                        err
                    );
                }
            }
            Some(Action_oneof_details::send_message(ref info)) => {
                if let Err(err) = self.execute_send_message(&info).await {
                    tracing::error!(
                        "Error while sending a message to a given channel for an action: {}",
                        err
                    );
                }
            }
            Some(Action_oneof_details::delete_messages(ref info)) => {
                if let Err(err) = self.execute_delete_messages(&info).await {
                    tracing::error!(
                        "Error while deleteing a message to a given channel for an action: {}",
                        err
                    );
                }
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
                .execute(self.storage().sql())
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

    fn execute_escalate<'a>(
        &'a self,
        action: &'a Action,
        info: &'a EscalateMember,
    ) -> BoxFuture<'a, Result<()>> {
        async move {
            let guild_id = GuildId::new(action.get_guild_id()).unwrap();
            let user_id = UserId::new(action.get_user_id()).unwrap();
            let manager = EscalationManager::new(self.clone());
            let guild = manager.guild(guild_id).await?;
            let history = guild.fetch_history(user_id).await?;
            history
                .apply_delta(
                    /*authorizer=*/ &self.current_user,
                    /*reason=*/ action.get_reason(),
                    /*diff=*/ info.get_amount() as i64,
                    /*execute=*/ info.get_amount() >= 0,
                )
                .await?;
            Ok(())
        }
        .boxed()
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
            .fetch_one(self.storage().sql())
            .await?;

        if info.get_role_ids().is_empty() {
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

    async fn execute_delete_messages(&self, info: &DeleteMessages) -> Result<()> {
        let channel_id = ChannelId::new(info.get_channel_id()).unwrap();
        let message_ids: Vec<MessageId> =
            info.message_ids.iter().cloned().filter_map(MessageId::new).collect();
        match message_ids.len() {
            0 => return Ok(()),
            1 => {
                self.http
                    .delete_message(channel_id, message_ids[0])
                    .exec()
                    .await?;
            }
            _ => {
                self.http
                    .delete_messages(channel_id, &message_ids)
                    .exec()
                    .await?;
            }
        }
        Ok(())
    }
}
