#![allow(clippy::expect_used)]

use super::{context, verifier::*};
use anyhow::Result;
use async_trait::async_trait;
use chrono::Duration;
use chrono::offset::Utc;
use hourai::models::{Snowflake, user::User};
use hourai_sql::{Ban, SqlPool, Username, VerificationBan};
use regex::Regex;

lazy_static! {
    static ref DELETED_USERNAME_MATCH: Regex =
        Regex::new("Deleted User [0-9a-fA-F]{8}").expect("Valid deleted user regex");
    static ref LOOSE_DELETED_USERNAME_MATCH: Regex =
        Regex::new("(?i).*Deleted.*User.*").expect("Valid deleted user regex");
}

fn is_user_deleted(user: &User) -> bool {
    user.avatar.is_none() && DELETED_USERNAME_MATCH.is_match(user.name.as_str())
}

struct DeletedUserRejector(SqlPool);

#[async_trait]
impl Verifier for DeletedUserRejector {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if is_user_deleted(&ctx.member().user) {
            ctx.add_rejection_reason(
                "Deleted users cannot be active on Discord. User has been \
                deleted by Discord of their own accord or for Trust and \
                Safety reasons, or is faking account deletion.",
            );
        }

        let usernames: Vec<Username> = Username::fetch(ctx.member().user.id, None)
            .fetch_all(&self.0)
            .await?;

        for username in usernames {
            let name = username.name.as_str();
            let is_deleted = DELETED_USERNAME_MATCH.is_match(name);
            if !is_deleted && LOOSE_DELETED_USERNAME_MATCH.is_match(name) {
                ctx.add_rejection_reason(format!(
                    "\"{}\" does not match Discord\'s deletion patterns. User may have \
                             attempted to fake account deletion.",
                    name
                ));
            } else if is_deleted && username.discriminator.map(|d| d < 100).unwrap_or(false) {
                let disc = username.discriminator.unwrap_or(0);
                ctx.add_rejection_reason(format!(
                    "\"{}#{:04}\" has an unusual discriminator for a deleted user. These \
                             are randomly generated. User may have attempted to fake account \
                             deletion.",
                    name, disc
                ));
            }
        }

        Ok(())
    }
}

struct BannedUserRejector {
    sql: SqlPool,
    min_guild_size: u64,
}

#[async_trait]
impl Verifier for BannedUserRejector {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        let bans: Vec<Ban> = Ban::fetch_user_bans(ctx.member().user.id)
            .fetch_all(&self.sql)
            .await?;

        let mut reasons: Vec<Option<String>> = Vec::new();
        for ban in bans {
            let count = hourai_sql::Member::count_guild_members(
                ban.guild_id(),
                /*include_bots=*/ false,
            )
            .fetch_one(&self.sql)
            .await?;
            if count.0 as u64 >= self.min_guild_size {
                reasons.push(ban.reason);
            }
        }

        if !reasons.is_empty() {
            let mut reason = format!("Banned from {} servers", reasons.len());
            let list: String = reasons
                .into_iter()
                .flatten()
                .collect::<Vec<String>>()
                .join("\n");
            if !list.is_empty() {
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
impl Verifier for BannedUsernameRejector {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        let name_bans =
            VerificationBan::fetch_by_name(ctx.guild_id(), ctx.member().user.name.as_str())
                .fetch_all(&self.0)
                .await?;
        for ban in name_bans {
            let mut reason = format!(
                "Exact username match with banned user: {}#{}",
                ban.name, ban.discriminator
            );
            if let Some(ban_reason) = ban.reason {
                reason.push_str(format!(" (Ban Reason: {})", ban_reason).as_str());
            }
            ctx.add_rejection_reason(reason);
        }

        if let Some(ref avatar) = ctx.member().user.avatar {
            let avatar_bans = VerificationBan::fetch_by_avatar(ctx.guild_id(), *avatar)
                .fetch_all(&self.0)
                .await?;
            for ban in avatar_bans {
                let mut reason = format!(
                    "Exact avatar match with banned user: {}#{}",
                    ban.name, ban.discriminator
                );
                if let Some(ban_reason) = ban.reason {
                    reason.push_str(format!(" (Ban Reason: {})", ban_reason).as_str());
                }
                ctx.add_rejection_reason(reason);
            }
        }

        Ok(())
    }
}

pub struct UsernameMatchRejector {
    sql: SqlPool,
    matches: Vec<(String, Regex)>,
    prefix: String,
}

impl UsernameMatchRejector {
    pub fn new(
        sql: SqlPool,
        prefix: impl Into<String>,
        matches: impl IntoIterator<Item = impl Into<String>>,
    ) -> Result<Self> {
        let mut regexes = Vec::new();
        for input in matches {
            let input_str = input.into();
            let regex = Regex::new(&Self::generalize_filter(&input_str))?;
            regexes.push((input_str, regex));
        }
        Ok(Self {
            sql,
            matches: regexes,
            prefix: prefix.into(),
        })
    }

    fn generalize_filter(base: &str) -> String {
        let mut res = String::from("(?i)");
        for ch in base.chars() {
            if ch.is_alphanumeric() {
                res.push(ch);
                res.push('+');
            } else {
                res.push(ch);
            }
        }
        res
    }
}

#[async_trait]
pub trait StringMatchRejector: Send + Sync {
    type Key;
    fn regexes(&self) -> Vec<(Self::Key, Regex)>;
    async fn criteria(&self, ctx: &context::VerificationContext) -> Result<Vec<String>>;
    fn reason(&self, key: &Self::Key, matched: &str) -> String;
}

#[async_trait]
impl StringMatchRejector for UsernameMatchRejector {
    type Key = String;

    fn regexes(&self) -> Vec<(Self::Key, Regex)> {
        self.matches.clone()
    }

    async fn criteria(&self, ctx: &context::VerificationContext) -> Result<Vec<String>> {
        Ok(Username::fetch(ctx.member().user.id, Some(20))
            .fetch_all(&self.sql)
            .await?
            .into_iter()
            .map(|un| un.name)
            .collect())
    }

    fn reason(&self, key: &Self::Key, matched: &str) -> String {
        format!("{}(Matches: {}): {}", self.prefix, key, matched)
    }
}

#[async_trait]
impl<T: StringMatchRejector + Send + Sync> Verifier for T {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        let criteria = self.criteria(ctx).await?;
        let regexes = self.regexes();
        for check in criteria {
            for (key, regex) in &regexes {
                if regex.is_match(check.as_str()) {
                    let reason = self.reason(key, check.as_str());
                    ctx.add_rejection_reason(reason);
                }
            }
        }

        Ok(())
    }
}

pub struct NewAccountRejector(Duration);

#[async_trait]
impl Verifier for NewAccountRejector {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if ctx.member().created_at() - Utc::now() < self.0 {
            let human_lookback = humantime::format_duration(self.0.to_std().unwrap_or_default());
            ctx.add_rejection_reason(format!("Account created less than {} ago.", human_lookback));
        }
        Ok(())
    }
}

pub fn new_account(lookback: Duration) -> BoxedVerifier {
    Box::new(NewAccountRejector(lookback))
}

pub fn no_avatar() -> BoxedVerifier {
    Box::new(NoAvatarRejector)
}

pub struct NoAvatarRejector;

#[async_trait]
impl Verifier for NoAvatarRejector {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if ctx.member().user.avatar.is_none() {
            ctx.add_rejection_reason("User has no avatar.");
        }
        Ok(())
    }
}

pub fn banned_user(sql: SqlPool, min_guild_size: u64) -> BoxedVerifier {
    Box::new(BannedUserRejector {
        sql,
        min_guild_size,
    })
}

pub fn banned_username(sql: SqlPool) -> BoxedVerifier {
    Box::new(BannedUsernameRejector(sql))
}

pub fn deleted_user(sql: SqlPool) -> BoxedVerifier {
    Box::new(DeletedUserRejector(sql))
}

#[allow(clippy::expect_used)]
pub fn username_match(
    sql: SqlPool,
    prefix: impl Into<String>,
    filters: impl IntoIterator<Item = impl Into<String>>,
) -> BoxedVerifier {
    Box::new(UsernameMatchRejector::new(sql, prefix, filters).expect("valid regex patterns"))
}
