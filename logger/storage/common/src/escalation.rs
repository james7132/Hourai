use crate::{actions::ActionExecutor, Storage};
use anyhow::Result;
use chrono::{DateTime, Duration, Utc};
use hourai::{
    http,
    models::id::{
        marker::{GuildMarker, UserMarker},
        Id,
    },
    proto::{
        escalation::EscalationLadderRung,
        guild_configs::{LoggingConfig, ModerationConfig},
    },
};
use hourai::{
    models::user::User,
    proto::action::{Action, ActionSet},
};
use hourai_sql::{EscalationEntry, Executor, PendingDeescalation};
use std::{
    cmp::{max, min},
    collections::HashSet,
    sync::Arc,
};
use thiserror::Error;

pub struct Escalation {
    pub current_level: i64,
    pub entry: EscalationEntry,
    pub current_rung: Option<EscalationLadderRung>,
    pub next_rung: Option<EscalationLadderRung>,
    pub expiration: Option<DateTime<Utc>>,
}

impl Escalation {
    pub fn expiration(&self) -> String {
        self.expiration
            .map(|exp| format!("<t:{}:R>", exp.timestamp()))
            .unwrap_or_else(|| "Never".into())
    }
}

#[derive(Debug, Error)]
pub enum EscalationError {
    #[error("A non-empty reason must be provided for any escalation.")]
    NoReason,
    #[error("No escalation ladder has been configured.")]
    NoLadderConfigured,
}

#[derive(Clone)]
pub struct EscalationManager(ActionExecutor);

impl EscalationManager {
    pub fn new(executor: ActionExecutor) -> Self {
        Self(executor)
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<http::Client> {
        self.0.http()
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        self.0.storage()
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        &self.0
    }

    pub async fn guild(&self, guild_id: Id<GuildMarker>) -> Result<GuildEscalationManager> {
        let config: ModerationConfig = self
            .storage()
            .redis()
            .guild(guild_id)
            .configs()
            .get()
            .await?;

        Ok(GuildEscalationManager {
            guild_id,
            manager: self.clone(),
            config,
        })
    }
}

#[derive(Clone)]
pub struct GuildEscalationManager {
    guild_id: Id<GuildMarker>,
    manager: EscalationManager,
    config: ModerationConfig,
}

impl GuildEscalationManager {
    #[inline(always)]
    pub fn guild_id(&self) -> Id<GuildMarker> {
        self.guild_id
    }

    #[inline(always)]
    pub fn config(&self) -> &ModerationConfig {
        &self.config
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<http::Client> {
        self.manager.http()
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        self.manager.storage()
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        self.manager.executor()
    }

    pub async fn fetch_history(&self, user_id: Id<UserMarker>) -> Result<EscalationHistory> {
        let entries = EscalationEntry::fetch(self.guild_id, user_id)
            .fetch_all(self.storage().sql())
            .await?;
        Ok(EscalationHistory {
            user_id,
            manager: self.clone(),
            entries,
        })
    }
}

pub struct EscalationHistory {
    user_id: Id<UserMarker>,
    manager: GuildEscalationManager,
    entries: Vec<EscalationEntry>,
}

impl EscalationHistory {
    #[inline(always)]
    pub fn user_id(&self) -> Id<UserMarker> {
        self.user_id
    }

    #[inline(always)]
    pub fn guild_id(&self) -> Id<GuildMarker> {
        self.manager.guild_id()
    }

    #[inline(always)]
    pub fn config(&self) -> &ModerationConfig {
        self.manager.config()
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<http::Client> {
        self.manager.http()
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        self.manager.storage()
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        self.manager.executor()
    }

    /// The current escalation level of the guild member.
    pub fn current_level(&self) -> i64 {
        let mut level: i64 = -1;
        for entry in self.entries() {
            level = std::cmp::max(-1, level + entry.level_delta as i64);
        }
        level
    }

    /// Iterates through all of the current entries for the user
    #[inline(always)]
    pub fn entries(&self) -> impl Iterator<Item = &EscalationEntry> {
        self.entries.iter()
    }

    #[inline(always)]
    pub async fn escalate(&self, authorizer: &User, reason: &str) -> Result<Escalation> {
        self.apply_delta(authorizer, reason, 1, true).await
    }

    #[inline(always)]
    pub async fn deescalate(&self, authorizer: &User, reason: &str) -> Result<Escalation> {
        self.apply_delta(authorizer, reason, -1, false).await
    }

    pub async fn apply_delta(
        &self,
        authorizer: &User,
        reason: &str,
        diff: i64,
        execute: bool,
    ) -> Result<Escalation> {
        if reason.is_empty() {
            anyhow::bail!(EscalationError::NoReason);
        } else if self.config().get_escalation_ladder().get_rung().is_empty() {
            anyhow::bail!(EscalationError::NoLadderConfigured);
        }

        let current_level = max(-1, self.current_level() + diff);
        let current_rung = self.get_rung(current_level);
        let mut actions = ActionSet::new();
        if execute {
            for rung_action in current_rung.as_ref().unwrap().get_action() {
                let mut action = rung_action.clone();
                action.set_user_id(self.user_id().get());
                action.set_guild_id(self.guild_id().get());
                action.set_reason(reason.to_string());
                self.executor().execute_action(&action).await?;
                actions.mut_action().push(action);
            }
        } else {
            let mut action = Action::new();
            action.set_user_id(self.user_id().get());
            action.set_guild_id(self.guild_id().get());
            action.set_reason(reason.to_string());
            action.mut_escalate().set_amount(diff);
            actions.mut_action().push(action);
        }

        let display_name = match current_rung {
            Some(rung) if diff >= 0 => rung.get_display_name(),
            _ => "Deescalate",
        };

        let entry = self.create_entry(&authorizer, actions, display_name, diff);
        let mut txn = self.storage().sql().begin().await?;
        let entry_id: i32 = entry.insert().fetch_one(&mut txn).await?.0;

        // Schedule the pending deescalation
        let mut expiration = None;
        if let Some(rung) = current_rung {
            if rung.has_deescalation_period() {
                expiration =
                    Some(Utc::now() + Duration::seconds(rung.get_deescalation_period() as i64));
                let pending = PendingDeescalation {
                    guild_id: self.guild_id().get() as i64,
                    user_id: self.user_id().get() as i64,
                    expiration: expiration.unwrap(),
                    amount: -1,
                    entry_id,
                };
                txn.execute(pending.insert()).await?;
            }
        } else {
            txn.execute(PendingDeescalation::delete(self.guild_id(), self.user_id()))
                .await?;
        }
        txn.commit().await?;

        let escalation = Escalation {
            current_level,
            entry,
            current_rung: current_rung.cloned(),
            next_rung: self.get_rung(current_level + 1).cloned(),
            expiration,
        };

        self.log_to_modlog(&escalation, diff).await?;

        Ok(escalation)
    }

    fn get_rung(&self, level: i64) -> Option<&EscalationLadderRung> {
        if level < 0 {
            None
        } else {
            let rungs = self.config().get_escalation_ladder().get_rung();
            let idx = min(level as usize, rungs.len() - 1);
            Some(&rungs[idx])
        }
    }

    fn create_entry(
        &self,
        authorizer: &User,
        actions: ActionSet,
        display_name: &str,
        diff: i64,
    ) -> EscalationEntry {
        let authorizer_name = format!("{}#{:04}", authorizer.name, authorizer.discriminator);
        EscalationEntry {
            guild_id: self.guild_id().get() as i64,
            subject_id: self.user_id().get() as i64,
            authorizer_id: authorizer.id.get() as i64,
            authorizer_name: authorizer_name,
            display_name: display_name.to_owned(),
            timestamp: Utc::now(),
            action: actions.into(),
            level_delta: diff as i32,
        }
    }

    async fn log_to_modlog(&self, escalation: &Escalation, diff: i64) -> Result<()> {
        let config: LoggingConfig = self
            .storage()
            .redis()
            .guild(self.guild_id())
            .configs()
            .get()
            .await?;
        let modlog_id = Id::new(config.get_modlog_channel_id());
        let arrow = if diff > 0 { "up" } else { "down" };
        let esc = if diff > 0 { "escalated" } else { "deescalated" };
        let reasons: HashSet<&str> = escalation
            .entry
            .action
            .0
            .get_action()
            .iter()
            .map(|a| a.get_reason())
            .collect();
        let reasons = reasons.into_iter().collect::<Vec<_>>().join("; ");
        let msg = format!(
            ":arrow_{}: **<@{}> {} <@{}>**\nReason: {}\nAction: {}\nExpiration: {}",
            arrow,
            escalation.entry.authorizer_id,
            esc,
            escalation.entry.subject_id,
            reasons,
            escalation.entry.display_name,
            escalation.expiration()
        );
        self.http()
            .create_message(modlog_id)
            .content(&msg)?
            .await?;

        Ok(())
    }
}
