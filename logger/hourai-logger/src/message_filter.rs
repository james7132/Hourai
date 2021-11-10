use crate::message_logging;
use anyhow::Result;
use hourai::proto::guild_configs::*;
use hourai::{
    models::{
        id::{ChannelId, RoleId},
        message::MessageLike,
        user::UserLike,
        Snowflake,
    },
    util::mentions,
};
use hourai_redis::{CachedMessage, GuildConfig};
use hourai_sql::Member;
use hourai_storage::actions::ActionExecutor;
use regex::{Regex, RegexSet};
use std::collections::HashSet;

lazy_static! {
    static ref SLUR_REGEX: RegexSet = generalize_filters(SLURS);
    static ref DISCORD_INVITE_REGEX: Regex = Regex::new("discord.gg/([a-zA-Z0-9]+)").unwrap();
}

const SLURS: &[&str] = &[
    "nigger", "nigga", "tarskin", "tranny", "trannie", "redskin", "faggot", "chink", "kike",
    "dyke", "gook", "wigger",
];

pub async fn check_message(executor: &ActionExecutor, message: &impl MessageLike) -> Result<bool> {
    let guild_id = if let Some(guild_id) = message.guild_id() {
        guild_id
    } else {
        return Ok(false);
    };
    let mut storage = executor.storage().clone();
    let config: ModerationConfig = GuildConfig::fetch_or_default(guild_id, &mut storage).await?;

    let mut redis = storage.redis().clone();
    let member = Member::fetch(guild_id, message.author().id())
        .fetch_one(executor.storage())
        .await;
    let moderator = if let Ok(member) = member {
        hourai_storage::is_moderator(guild_id, member.role_ids(), &mut redis).await?
    } else {
        false
    };

    for rule in config.get_message_filter().get_rules() {
        let reasons = get_filter_reasons(moderator, message, rule.get_criteria()).await?;
        if !reasons.is_empty() {
            apply_rule(message, rule.clone(), reasons, executor).await?;
            return Ok(rule.get_delete_message());
        }
    }

    Ok(false)
}

fn generalize_filters(filters: &[&str]) -> RegexSet {
    let generalized = filters
        .iter()
        .map(|filter| generalize_filter(*filter))
        .collect::<Vec<String>>();
    RegexSet::new(&generalized).unwrap()
}

fn generalize_filter(filter: &str) -> String {
    regex::escape(filter)
        .chars()
        .map(|chr| {
            if chr.is_alphanumeric() {
                format!("{}+", chr)
            } else {
                chr.into()
            }
        })
        .collect::<String>()
}

async fn apply_rule(
    message: &impl MessageLike,
    rule: MessageFilterRule,
    reasons: Vec<String>,
    executor: &ActionExecutor,
) -> Result<()> {
    let mut action_taken = None;
    let guild_id = message.guild_id().unwrap();
    let author_id = message.author().id();
    let channel_id = message.channel_id();
    let message_id = message.id();

    if rule.get_notify_moderator() {
        action_taken = Some(format!(
            "Message filter found notable message by <@{}> in <#{}>",
            author_id, channel_id
        ));

        tracing::info!(
            "Message filter notified moderator about message {} in channel {}",
            message.id(),
            message.channel_id()
        );
    }

    if rule.get_delete_message() {
        action_taken = Some(format!(
            "Message filter deleted a message by <@{}> in <#{}>",
            author_id, channel_id
        ));

        // Delete the message from the cache to avoid logging it when it gets deleted.
        CachedMessage::delete(message.channel_id(), message.id())
            .query_async(&mut executor.storage().clone())
            .await?;

        let http = executor.http().clone();
        tokio::spawn(async move {
            // TODO(james7132): DM the user that their message was deleted.
            let result = http.delete_message(channel_id, message_id).exec().await;
            if let Err(err) = result {
                tracing::error!(
                    "Error while deleting message {} in channel {} for message filter: {}",
                    message_id,
                    channel_id,
                    err
                );
                return;
            }
        });

        tracing::info!(
            "Message filter deleted message {} in channel {}",
            message.id(),
            message.channel_id()
        );
    }

    if !rule.additional_actions.is_empty() {
        let rule = rule.clone();
        let exec = executor.clone();
        tokio::spawn(async move {
            for action_template in rule.additional_actions.iter() {
                let mut action = action_template.clone();
                action.set_guild_id(guild_id.get());
                action.set_user_id(author_id.get());
                action.set_reason(format!("Triggered message filter: {}", rule.get_name()));
                if let Err(err) = exec.execute_action(&action).await {
                    tracing::error!("Error while running actions for message filter: {}", err);
                    break;
                }
            }
        });
    }

    if let Some(action_taken) = action_taken {
        let ping = if rule.get_notify_moderator() {
            let (_, ping) = hourai_storage::ping_online_mod(guild_id, executor.storage()).await?;
            ping
        } else {
            "".to_string()
        };

        let response = format!(
            "{} {}:\n```\n   - {}\n```",
            ping,
            action_taken,
            reasons.join("\n   - ")
        );

        let config: LoggingConfig =
            GuildConfig::fetch_or_default(guild_id, &mut executor.storage().redis().clone())
                .await?;
        if let Some(modlog_id) = ChannelId::new(config.get_modlog_channel_id()) {
            executor
                .http()
                .create_message(modlog_id)
                .content(&response)?
                .embeds(&[message_logging::message_to_embed(message)?.build()?])?
                .exec()
                .await?;
        }
    }

    Ok(())
}

async fn get_filter_reasons(
    moderator: bool,
    message: &impl MessageLike,
    criteria: &MessageFilterRule_Criteria,
) -> Result<Vec<String>> {
    let mut reasons = Vec::new();

    let is_bot = criteria.get_exclude_bots() && message.author().bot();
    let is_in_excluded_channel = criteria
        .get_excluded_channels()
        .contains(&message.channel_id().get());
    let is_moderator = criteria.get_exclude_moderators() && moderator;

    if is_bot || is_moderator || is_in_excluded_channel {
        return Ok(reasons);
    }

    match RegexSet::new(&criteria.matches) {
        Ok(regex) => {
            if regex.is_match(message.content()) {
                reasons.push(String::from("Message contains banned word or phrase."));
            }
        }
        Err(err) => {
            tracing::warn!(
                "Error while building regex for message filter: {}",
                err
            );
        }
    }

    if criteria.get_includes_slurs() {
        for word in message.content().split_whitespace() {
            if SLUR_REGEX.is_match(word) {
                reasons.push(format!("Message contains recognized racial slur: {}", word));
            }
            break;
        }
    }

    if criteria.get_includes_invite_links() {
        if DISCORD_INVITE_REGEX.is_match(message.content()) {
            reasons.push("Message contains Discord invite link.".into());
        }
    }

    if let Some(mentions) = criteria.mentions.as_ref() {
        get_mention_reason(message, mentions, &mut reasons);
    }
    if let Some(embeds) = criteria.embeds.as_ref() {
        get_embed_reason(message, embeds, &mut reasons);
    }

    Ok(reasons)
}

fn get_mention_reason(
    message: &impl MessageLike,
    criteria: &MentionFilterCriteria,
    reasons: &mut Vec<String>,
) {
    let users = mentions::get_user_mention_ids(message.content())
        .map(|id| id.get())
        .collect::<Vec<_>>();
    let roles = mentions::get_role_mention_ids(message.content())
        .map(|id| id.get())
        .collect::<Vec<_>>();
    let mut all = Vec::new();
    all.extend(users.iter().cloned());
    all.extend(roles.iter().cloned());

    check_limits("user mentions", users, criteria.get_user_mention(), reasons);
    check_limits("role mentions", roles, criteria.get_role_mention(), reasons);
    check_limits("mentions", all, criteria.get_any_mention(), reasons);
}

fn get_embed_reason(
    message: &impl MessageLike,
    criteria: &EmbedFilterCriteria,
    reasons: &mut Vec<String>,
) {
    let mut urls = HashSet::new();
    urls.extend(
        message
            .embeds()
            .iter()
            .filter_map(|embed| embed.url.clone()),
    );
    urls.extend(message.attachments().iter().map(|embed| embed.url.clone()));
    if criteria.has_max_embed_count() && urls.len() > criteria.get_max_embed_count() as usize {
        reasons.push(format!(
            "Message has {} embeds or attachments. More than the server maximum of {}.",
            urls.len(),
            criteria.get_max_embed_count()
        ));
    }
}

fn check_limits(
    name: &str,
    ids: Vec<u64>,
    limits: &MentionFilterCriteria_MentionLimits,
    reasons: &mut Vec<String>,
) {
    let unique = ids.iter().cloned().collect::<HashSet<_>>();
    if limits.has_maximum_total() {
        if ids.len() > limits.get_maximum_total() as usize {
            reasons.push(format!(
                "Total {} more than the server limit (seen: {}, limit: {}).",
                name,
                ids.len(),
                limits.get_maximum_total()
            ));
        }
    }

    if limits.has_maximum_unique() {
        if unique.len() > limits.get_maximum_unique() as usize {
            reasons.push(format!(
                "Unique {} more than the server limit (seen: {}, limit: {}).",
                name,
                ids.len(),
                limits.get_maximum_unique()
            ));
        }
    }
}
