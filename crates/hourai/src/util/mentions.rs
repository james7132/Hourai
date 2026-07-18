use crate::models::id::{Id, marker::*};
use regex::Regex;
use std::str::FromStr;
use std::sync::LazyLock;

static USER_MENTION_REGEX: LazyLock<Regex> = LazyLock::new(|| match Regex::new(r"<@!?(\d+)>") {
    Ok(re) => re,
    Err(_) => unreachable!(),
});
static ROLE_MENTION_REGEX: LazyLock<Regex> = LazyLock::new(|| match Regex::new(r"<@&(\d+)>") {
    Ok(re) => re,
    Err(_) => unreachable!(),
});
static CHANNEL_MENTION_REGEX: LazyLock<Regex> = LazyLock::new(|| match Regex::new(r"<@#(\d+)>") {
    Ok(re) => re,
    Err(_) => unreachable!(),
});

pub fn get_user_mention_ids(text: &str) -> impl Iterator<Item = Id<UserMarker>> + '_ {
    USER_MENTION_REGEX
        .find_iter(text)
        .filter_map(|hit| u64::from_str(hit.as_str()).ok())
        .map(Id::new)
}

pub fn get_role_mention_ids(text: &str) -> impl Iterator<Item = Id<RoleMarker>> + '_ {
    ROLE_MENTION_REGEX
        .find_iter(text)
        .filter_map(|hit| u64::from_str(hit.as_str()).ok())
        .map(Id::new)
}

pub fn get_channel_mention_ids(text: &str) -> impl Iterator<Item = Id<ChannelMarker>> + '_ {
    CHANNEL_MENTION_REGEX
        .find_iter(text)
        .filter_map(|hit| u64::from_str(hit.as_str()).ok())
        .map(Id::new)
}
