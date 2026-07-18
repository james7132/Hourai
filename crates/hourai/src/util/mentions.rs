#![allow(clippy::expect_used)]

use crate::models::id::{Id, marker::*};
use regex::Regex;
use std::str::FromStr;
use std::sync::LazyLock;

static USER_MENTION_REGEX: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"<@!?(\d+)>").expect("Valid user mention regex"));
static ROLE_MENTION_REGEX: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"<@&(\d+)>").expect("Valid role mention regex"));
static CHANNEL_MENTION_REGEX: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"<@#(\d+)>").expect("Valid channel mention regex"));

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
