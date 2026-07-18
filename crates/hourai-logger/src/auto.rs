use anyhow::Result;
use hourai::{
    models::{
        MessageLike, Snowflake, UserLike,
        guild::Member,
        id::{Id, marker::*},
        user::User,
    },
    proto::{
        auto_config::{AutoConfig, MessageEvent_Type},
        util::FilterSettings,
    },
};
use hourai_storage::actions::ActionExecutor;
use regex::Regex;

fn meets_filter(val: Option<&str>, filter: Option<&FilterSettings>) -> bool {
    let val = match val {
        Some(v) => v,
        None => return false,
    };
    let filter = match filter {
        Some(f) => f,
        None => return true,
    };

    let meets_blacklist = filter
        .get_blacklist()
        .iter()
        .any(|p| Regex::new(p).map(|r| r.is_match(val)).unwrap_or(false));
    let meets_whitelist = filter
        .get_whitelist()
        .iter()
        .any(|p| Regex::new(p).map(|r| r.is_match(val)).unwrap_or(false));

    if !filter.get_blacklist().is_empty() {
        !meets_blacklist || meets_whitelist
    } else if !filter.get_whitelist().is_empty() {
        meets_whitelist
    } else {
        true
    }
}

pub struct AutoEngine;

impl AutoEngine {
    pub async fn on_message(
        actions: &ActionExecutor,
        config: &AutoConfig,
        msg: &impl MessageLike,
        is_edit: bool,
    ) -> Result<()> {
        let guild_id = match msg.guild_id() {
            Some(g) => g,
            None => return Ok(()),
        };
        if msg.author().bot() {
            return Ok(());
        }

        let event_mask = if is_edit {
            MessageEvent_Type::MESSAGE_EDITS
        } else {
            MessageEvent_Type::MESSAGE_CREATES
        };

        let mut delete = false;
        let mut to_execute = Vec::new();

        if let Some(events) = config.guild_events.as_ref() {
            for evt in events.get_on_message() {
                let evt_type = evt.get_field_type();
                if evt_type != MessageEvent_Type::ALL_MESSAGES && evt_type != event_mask {
                    continue;
                }
                if !meets_filter(Some(msg.content()), evt.content_filter.as_ref()) {
                    continue;
                }
                if evt.get_delete_message() {
                    delete = true;
                }
                for action in evt.get_action() {
                    let mut act = action.clone();
                    act.set_guild_id(guild_id.get());
                    act.set_user_id(msg.author().id().get());
                    to_execute.push(act);
                }
            }
        }

        for act in to_execute {
            let _ = actions.execute_action(&act).await;
        }

        if delete {
            let _ = actions
                .http()
                .delete_message(msg.channel_id(), msg.id())
                .await;
        }

        Ok(())
    }

    pub async fn on_member_join(
        actions: &ActionExecutor,
        config: &AutoConfig,
        guild_id: Id<GuildMarker>,
        member: &Member,
    ) -> Result<()> {
        if member.user.bot {
            return Ok(());
        }
        let mut to_execute = Vec::new();
        if let Some(events) = config.guild_events.as_ref() {
            for evt in events.get_on_join() {
                if !meets_filter(Some(&member.user.name), evt.username_filter.as_ref()) {
                    continue;
                }
                for action in evt.get_action() {
                    let mut act = action.clone();
                    act.set_guild_id(guild_id.get());
                    act.set_user_id(member.user.id.get());
                    to_execute.push(act);
                }
            }
        }
        for act in to_execute {
            let _ = actions.execute_action(&act).await;
        }
        Ok(())
    }

    pub async fn on_member_remove(
        actions: &ActionExecutor,
        config: &AutoConfig,
        guild_id: Id<GuildMarker>,
        user: &User,
    ) -> Result<()> {
        if user.bot {
            return Ok(());
        }
        let mut to_execute = Vec::new();
        if let Some(events) = config.guild_events.as_ref() {
            for evt in events.get_on_leave() {
                if !meets_filter(Some(&user.name), evt.username_filter.as_ref()) {
                    continue;
                }
                for action in evt.get_action() {
                    let mut act = action.clone();
                    act.set_guild_id(guild_id.get());
                    act.set_user_id(user.id.get());
                    to_execute.push(act);
                }
            }
        }
        for act in to_execute {
            let _ = actions.execute_action(&act).await;
        }
        Ok(())
    }

    pub async fn on_member_ban(
        actions: &ActionExecutor,
        config: &AutoConfig,
        guild_id: Id<GuildMarker>,
        user: &User,
    ) -> Result<()> {
        if user.bot {
            return Ok(());
        }
        let mut to_execute = Vec::new();
        if let Some(events) = config.guild_events.as_ref() {
            for evt in events.get_on_ban() {
                if !meets_filter(Some(&user.name), evt.username_filter.as_ref()) {
                    continue;
                }
                for action in evt.get_action() {
                    let mut act = action.clone();
                    act.set_guild_id(guild_id.get());
                    act.set_user_id(user.id.get());
                    to_execute.push(act);
                }
            }
        }
        for act in to_execute {
            let _ = actions.execute_action(&act).await;
        }
        Ok(())
    }
}
