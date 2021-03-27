use serde::Serialize;
use twilight_model::{
    guild::PremiumTier,
    id::{GuildId, UserId},
};

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct CachedGuild {
    pub id: GuildId,
    pub name: Box<str>,
    pub description: Option<Box<str>>,
    pub features: Vec<Box<str>>,
    pub icon: Option<Box<str>>,
    pub member_count: Option<u64>,
    pub owner_id: UserId,
    pub premium_subscription_count: Option<u64>,
    pub premium_tier: PremiumTier,
    pub unavailable: bool,
    pub vanity_url_code: Option<Box<str>>,
}
