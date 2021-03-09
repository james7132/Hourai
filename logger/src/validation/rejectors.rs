use async_trait::async_trait;
use chrono::Duration;
use chrono::offset::Utc;
use crate::cache::InMemoryCache;
use crate::db::{SqlPool, Username, Ban, ValidationBan};
use crate::models::Snowflake;
use dashmap::DashMap;
use std::collections::HashMap;
use regex::Regex;
use super::{*, context};
use twilight_model::user::User;

lazy_static! {
    static ref DELETED_USERNAME_MATCH: Regex = Regex::new("Deleted User [0-9a-fA-F]{8}").unwrap();
    static ref LOOSE_DELETED_USERNAME_MATCH: Regex = Regex::new("(?i).*Deleted.*User.*").unwrap();
}

fn is_user_deleted(user: &User) -> bool {
    user.avatar.is_none() && DELETED_USERNAME_MATCH.is_match(user.name.as_str())
}

struct DeletedUserRejector(SqlPool);

#[async_trait]
impl Validator for DeletedUserRejector {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        if is_user_deleted(&ctx.member().user) {
            ctx.add_rejection_reason(
                "Deleted users cannot be active on Discord. User has been \
                deleted by Discord of their own accord or for Trust and \
                Safety reasons, or is faking account deletion.");
        }

        let usernames: Vec<Username> = Username::fetch(ctx.member().user.id, None)
                                                .fetch_all(&self.0)
                                                .await?;

        for username in usernames {
            let name = username.name.as_str();
            let is_deleted = DELETED_USERNAME_MATCH.is_match(name);
            if !is_deleted && LOOSE_DELETED_USERNAME_MATCH.is_match(name) {
                ctx.add_rejection_reason(
                    format!("\"{}\" does not match Discord\'s deletion patterns. User may have \
                             attemtped to fake account deletion.", name));
            } else if is_deleted && username.discriminator.map(|d| d < 100).unwrap_or(false) {
                ctx.add_rejection_reason(
                    format!("\"{}#{:04}\" has a unusual discriminator for a deleted user. These \
                             are randomly generated. User may have attemtped to fake account \
                             deletion.", name, username.discriminator.unwrap()));
            }
        }

        Ok(())
    }
}

struct BannedUserRejector {
    sql: SqlPool,
    cache: InMemoryCache,
    min_guild_size: u64
}

#[async_trait]
impl Validator for BannedUserRejector {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        let bans: Vec<Ban> = Ban::fetch_user_bans(ctx.member().user.id)
                                 .fetch_all(&self.sql)
                                 .await?;

        let mut reasons: Vec<Option<String>> = Vec::new();
        for ban in bans {
            if let Some(guild) = self.cache.guild(ban.guild_id()) {
                if guild.member_count.unwrap_or(0) < self.min_guild_size {
                    continue;
                }
            } else {
                continue;
            }
            reasons.push(ban.reason);
        }

        if reasons.len() > 0 {
            let mut reason = format!("Banned from {} servers", reasons.len());
            let list: String =
                reasons.into_iter()
                       .filter_map(|r| r)
                       .collect::<Vec<String>>()
                       .join("\n");
            if list.len() > 0 {
                reason.push_str(" for the following reasons\n");
                reason.push_str(list.as_str());
            }
            ctx.add_rejection_reason(reason);
        }

        Ok(())
    }
}

struct BannedUsernameRejector(SqlPool);

#[async_trait]
impl Validator for BannedUsernameRejector {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        let name_bans = ValidationBan::fetch_by_name(ctx.member().guild_id,
                                                     ctx.member().user.name.as_str())
                                      .fetch_all(&self.0)
                                      .await?;
        for ban in name_bans {
            let mut reason = format!("Exact username match with banned user: {}#{}",
                                     ban.name, ban.discriminator);
            if let Some(ban_reason) = ban.reason {
                reason.push_str(format!(" (Ban Reason: {})", ban_reason).as_str());
            }
            ctx.add_rejection_reason(reason);
        }

        if ctx.member().user.avatar.is_none() {
            return Ok(());
        }

        let avatar_bans = ValidationBan::fetch_by_name(ctx.member().guild_id,
                                                       ctx.member().user.avatar.as_ref().unwrap())
                                        .fetch_all(&self.0)
                                        .await?;
        for ban in avatar_bans {
            let mut reason = format!("Exact avatar match with banned user: {}#{}",
                                     ban.name, ban.discriminator);
            if let Some(ban_reason) = ban.reason {
                reason.push_str(format!(" (Ban Reason: {})", ban_reason).as_str());
            }
            ctx.add_rejection_reason(reason);
        }

        Ok(())
    }
}

#[async_trait]
pub trait StringMatchRejector : Sync {
    type Key;
    fn regexes(&self) -> Vec<(Self::Key, Regex)>;
    async fn criteria(&self, ctx: &context::ValidationContext) -> Result<Vec<String>>;
    fn reason(&self, key: &Self::Key, matched: &str) -> String;
}

#[async_trait]
impl<T : StringMatchRejector> Validator for T {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        let criteria = self.criteria(&ctx).await?;
        let regexes = self.regexes();
        for check in criteria{
            for (key, regex) in &regexes {
                if let Some(reg_match) = regex.find(check.as_str()) {
                    let reason = self.reason(&key, check.as_str());
                    ctx.add_rejection_reason(reason);
                }
            }
        }

        Ok(())
    }
}

pub struct UsernameMatchRejector {
    sql: SqlPool,
    matches: DashMap<String, Regex>,
    prefix: String
}

impl UsernameMatchRejector {
    pub fn new(
        sql: SqlPool,
        prefix: impl Into<String>,
        matches: Vec<String>
    ) -> Result<Self> {
        Ok(Self {
            sql,
            matches: Self::compile(matches)?,
            prefix: prefix.into()
        })
    }

    fn compile(base: Vec<String>) -> Result<DashMap<String, Regex>> {
        let matches: DashMap<String, Regex> = DashMap::new();
        for input in base {
            matches.insert(input.clone(), Regex::new(Self::generalize_filter(input).as_str())?);
        }
        Ok(matches)
    }

    fn generalize_filter(base: String) -> String {
        base.chars()
            .flat_map(|ch| {
                if ch.is_alphanumeric() {
                    vec![ch, '+']
                } else {
                    vec![ch]
                }
            })
            .collect()
    }
}

#[async_trait]
impl StringMatchRejector for UsernameMatchRejector {
    type Key = String;

    fn regexes(&self) -> Vec<(Self::Key, Regex)> {
        self.matches.iter().map(|kv| (kv.key().clone(), kv.value().clone())).collect()
    }

    async fn criteria(&self, ctx: &context::ValidationContext) -> Result<Vec<String>> {
        Ok(Username::fetch(ctx.member().user.id, Some(20))
                     .fetch_all(&self.sql)
                     .await?
                     .into_iter()
                     .map(|un| un.name)
                     .collect())
    }

    fn reason(&self, key: &Self::Key, matched: &str) -> String {
        format!("{} (Matches: {}): {}", self.prefix, key, matched)
    }
}

pub(super) fn new_account(lookback: Duration) -> BoxedValidator {
    let human_lookback = humantime::format_duration(lookback.to_std().unwrap());
    GenericValidator::new_rejector(
        format!("Account created less than {} ago.", human_lookback),
        move |ctx| Ok(ctx.member().created_at() - Utc::now() < lookback))
}

pub(super) fn no_avatar() -> BoxedValidator {
    GenericValidator::new_rejector(
        "User has no avatar.",
        move |ctx| Ok(ctx.member().user.avatar.is_none()))
}

pub(super) fn banned_user(sql: SqlPool, cache: InMemoryCache, min_guild_size: u64) ->
    BoxedValidator {
    Box::new(BannedUserRejector { sql, cache, min_guild_size })
}

pub(super) fn banned_username(sql: SqlPool) -> BoxedValidator {
    Box::new(BannedUsernameRejector(sql))
}

pub(super) fn deleted_user(sql: SqlPool) -> BoxedValidator {
    Box::new(DeletedUserRejector(sql))
}
