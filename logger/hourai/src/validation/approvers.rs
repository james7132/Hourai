use super::{*, context};
use async_trait::async_trait;
use crate::cache::InMemoryCache;
use twilight_model::id::UserId;
use twilight_model::user::{User, UserFlags};
use std::collections::HashSet;

lazy_static! {
    static ref VERIFIED_FEATURE: String = "VERIFIED".to_owned();
}

struct DistinguishedUserValidator(InMemoryCache);

#[async_trait]
impl Validator for DistinguishedUserValidator {

    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        let flags = ctx.member().user.flags.unwrap_or(UserFlags::empty());
        if flags.contains(UserFlags::DISCORD_EMPLOYEE) {
            ctx.add_approval_reason("User is Discord Staff.");
        }
        if flags.contains(UserFlags::DISCORD_PARTNER) {
            ctx.add_approval_reason("User is a Discord Partner.");
        }
        if flags.contains(UserFlags::VERIFIED_BOT_DEVELOPER) {
            ctx.add_approval_reason("User is a verified bot developer.");
        }
        // TODO(james7123): This will not scale to multiple processes
        let member_id = ctx.member().user.id;
        for guild_id in self.0.guilds() {
            if let Some(guild) = self.0.guild(guild_id) {
                if guild.owner_id == member_id && guild.features.contains(&VERIFIED_FEATURE) {
                    ctx.add_approval_reason("User is a verified bot developer.");
                }
            }
        }
        Ok(())
    }

}

pub fn user_has_nitro(user: &User) -> bool {
    let flag = user.flags.map(|f| f.contains(UserFlags::EARLY_SUPPORTER)).unwrap_or(false);
    let animated = user.avatar.as_ref().map(|a| a.starts_with("a_")).unwrap_or(false);
    flag || animated
}

pub(super) fn nitro() -> BoxedValidator {
    GenericValidator::new_approver(
        "User currently has or has had Nitro. Probably not a user bot.",
        |ctx| Ok(user_has_nitro(&ctx.member().user)))
}

pub(super) fn bot_owners(owners: impl IntoIterator<Item=UserId>) -> BoxedValidator {
    let owner_ids: HashSet<UserId> = owners.into_iter().collect();
    GenericValidator::new_approver(
        "User is an owner of this bot.",
        move |ctx| Ok(owner_ids.contains(&ctx.member().user.id)))
}

pub(super) fn bot() -> BoxedValidator {
    GenericValidator::new_approver(
        "User is an OAuth2 bot that can only be manually added by moderators.",
        |ctx| Ok(ctx.member().user.bot))
}

pub(super) fn distinguished_user(cache: InMemoryCache) -> BoxedValidator {
    Box::new(DistinguishedUserValidator(cache))
}
