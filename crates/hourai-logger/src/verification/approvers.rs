use super::{context, verifier::*};
use anyhow::Result;
use async_trait::async_trait;
use hourai::cache::InMemoryCache;
use hourai::models::id::{marker::UserMarker, Id};
use hourai::models::user::{PremiumType, User, UserFlags};
use std::collections::HashSet;

pub struct DistinguishedUserVerifier(#[allow(dead_code)] InMemoryCache);

#[async_trait]
impl Verifier for DistinguishedUserVerifier {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        let flags = ctx.member().user.flags.unwrap_or_else(UserFlags::empty);
        if flags.contains(UserFlags::STAFF) {
            ctx.add_approval_reason("User is Discord Staff.");
        }
        if flags.contains(UserFlags::PARTNER) {
            ctx.add_approval_reason("User is a Discord Partner.");
        }
        if flags.contains(UserFlags::VERIFIED_DEVELOPER) {
            ctx.add_approval_reason("User is a verified bot developer.");
        }
        Ok(())
    }
}

pub fn user_has_nitro(user: &User) -> bool {
    let premium = user
        .premium_type
        .map(|premium| premium != PremiumType::None)
        .unwrap_or(false);
    let flag = user
        .public_flags
        .map(|f| f.contains(UserFlags::PREMIUM_EARLY_SUPPORTER))
        .unwrap_or(false);
    let has_banner = user.banner.is_some();
    let animated = user
        .avatar
        .as_ref()
        .map(|a| a.is_animated())
        .unwrap_or(false);
    premium || has_banner || flag || animated
}

pub struct NitroApprover;

#[async_trait]
impl Verifier for NitroApprover {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if user_has_nitro(&ctx.member().user) {
            ctx.add_approval_reason(
                "User currently has or has had Nitro. Probably not a user bot.",
            );
        }
        Ok(())
    }
}

pub fn nitro() -> BoxedVerifier {
    Box::new(NitroApprover)
}

pub struct BotOwnerApprover(HashSet<Id<UserMarker>>);

#[async_trait]
impl Verifier for BotOwnerApprover {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if self.0.contains(&ctx.member().user.id) {
            ctx.add_approval_reason("User is an owner of this bot.");
        }
        Ok(())
    }
}

pub fn bot_owners(owners: impl IntoIterator<Item = Id<UserMarker>>) -> BoxedVerifier {
    Box::new(BotOwnerApprover(owners.into_iter().collect()))
}

pub struct BotApprover;

#[async_trait]
impl Verifier for BotApprover {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        if ctx.member().user.bot {
            ctx.add_approval_reason(
                "User is an OAuth2 bot that can only be manually added by moderators.",
            );
        }
        Ok(())
    }
}

pub fn bot() -> BoxedVerifier {
    Box::new(BotApprover)
}

pub fn distinguished_user(cache: InMemoryCache) -> BoxedVerifier {
    Box::new(DistinguishedUserVerifier(cache))
}
