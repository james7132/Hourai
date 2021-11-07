use anyhow::Result;
use chrono::{DateTime, Duration, Utc};
use hourai::{
    http,
    models::id::{ChannelId, GuildId, UserId},
    proto::{
        escalation::EscalationLadderRung,
        guild_configs::{LoggingConfig, ModerationConfig},
    },
};
use hourai::{
    models::guild::Member,
    proto::action::{Action, ActionSet},
};
use hourai_redis::{GuildConfig, RedisPool};
use hourai_sql::{
    actions::ActionExecutor, EscalationEntry, Executor, PendingDeescalation, SqlPool,
};
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

#[derive(Debug, Error)]
pub enum EscalationError {
    #[error("A non-empty reason must be provided for any escalation.")]
    NoReason,
    #[error("No escalation ladder has been configured.")]
    NoLadderConfigured,
}

#[derive(Clone)]
pub struct EscalationManager {
    actions: ActionExecutor,
    redis: RedisPool,
}

impl EscalationManager {
    pub fn new(executor: ActionExecutor, redis: RedisPool) -> Self {
        Self {
            actions: executor,
            redis,
        }
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<http::Client> {
        self.actions.http()
    }

    #[inline(always)]
    pub fn sql(&self) -> &SqlPool {
        self.actions.sql()
    }

    #[inline(always)]
    pub fn redis(&self) -> &RedisPool {
        &self.redis
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        &self.actions
    }

    pub async fn guild(&self, guild_id: GuildId) -> Result<GuildEscalationManager> {
        let config =
            GuildConfig::fetch_or_default::<ModerationConfig>(guild_id, &mut self.redis.clone())
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
    guild_id: GuildId,
    manager: EscalationManager,
    config: ModerationConfig,
}

impl GuildEscalationManager {
    #[inline(always)]
    pub fn guild_id(&self) -> GuildId {
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
    pub fn sql(&self) -> &SqlPool {
        self.manager.sql()
    }

    #[inline(always)]
    pub fn redis(&self) -> &RedisPool {
        self.manager.redis()
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        self.manager.executor()
    }

    pub async fn fetch_history(&self, user_id: UserId) -> Result<EscalationHistory> {
        let entries = EscalationEntry::fetch(self.guild_id, user_id)
            .fetch_all(self.manager.sql())
            .await?;
        Ok(EscalationHistory {
            user_id,
            manager: self.clone(),
            entries,
        })
    }
}

pub struct EscalationHistory {
    user_id: UserId,
    manager: GuildEscalationManager,
    entries: Vec<EscalationEntry>,
}

impl EscalationHistory {
    #[inline(always)]
    pub fn user_id(&self) -> UserId {
        self.user_id
    }

    #[inline(always)]
    pub fn guild_id(&self) -> GuildId {
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
    pub fn sql(&self) -> &SqlPool {
        self.manager.sql()
    }

    #[inline(always)]
    pub fn executor(&self) -> &ActionExecutor {
        self.manager.executor()
    }

    /// The current escalation level of the guild member.
    pub fn current_level(&self) -> i64 {
        let mut level: i64 = -1;
        for entry in self.entries() {
            level = std::cmp::max(0, level + entry.level_delta as i64);
        }
        level
    }

    /// Iterates through all of the current entries for the user
    #[inline(always)]
    pub fn entries(&self) -> impl Iterator<Item = &EscalationEntry> {
        self.entries.iter()
    }

    #[inline(always)]
    pub async fn escalate(
        &self,
        authorizer: &Member,
        reason: &str,
    ) -> Result<Escalation> {
        self.apply_delta(authorizer, reason, 1, true).await
    }

    #[inline(always)]
    pub async fn deescalate(
        &self,
        authorizer: &Member,
        reason: &str,
    ) -> Result<Escalation> {
        self.apply_delta(authorizer, reason, -1, false).await
    }

    pub async fn apply_delta(
        &self,
        authorizer: &Member,
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

        let display_name = if let Some(rung) = current_rung {
            rung.get_display_name()
        } else {
            "Deescalate"
        };

        let entry = self.create_entry(&authorizer, actions, display_name, diff);
        let mut txn = self.sql().begin().await?;
        let entry_id: i64 = entry.insert().fetch_one(&mut txn).await?.0;

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
        authorizer: &Member,
        actions: ActionSet,
        display_name: &str,
        diff: i64,
    ) -> EscalationEntry {
        let user = &authorizer.user;
        let authorizer_name = format!("{}#{:04}", user.name, user.discriminator);
        EscalationEntry {
            guild_id: self.guild_id().get() as i64,
            subject_id: self.user_id().get() as i64,
            authorizer_id: user.id.get() as i64,
            authorizer_name: authorizer_name,
            display_name: display_name.to_owned(),
            timestamp: Utc::now(),
            action: actions.into(),
            level_delta: diff as i32,
        }
    }

    async fn log_to_modlog(&self, escalation: &Escalation, diff: i64) -> Result<()> {
        let modlog_id = GuildConfig::fetch_or_default::<LoggingConfig>(
            self.guild_id(),
            &mut self.manager.redis().clone(),
        )
        .await?
        .get_modlog_channel_id();
        if let Some(modlog_id) = ChannelId::new(modlog_id) {
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
            let expiration = escalation.expiration.map(|exp| exp.to_rfc2822());
            let expiration = expiration.as_deref().unwrap_or("Never");
            let msg = format!(
                ":arrow_{}: **<@{}> {} <@{}>**\nReason: {}\nAction: {}\nExpiration: {}",
                arrow,
                escalation.entry.authorizer_id,
                esc,
                escalation.entry.subject_id,
                reasons,
                escalation.entry.display_name,
                expiration
            );
            self.http()
                .create_message(modlog_id)
                .content(&msg)?
                .exec()
                .await?;
        }

        Ok(())
    }
}
