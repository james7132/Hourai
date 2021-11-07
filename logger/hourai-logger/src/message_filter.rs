use anyhow::Result;
use hourai::proto::guild_configs::*;
use hourai::{
    models::{id::ChannelId, message::MessageLike, Snowflake},
    util::mentions,
};
use hourai_redis::{CachedMessage, GuildConfig};
use hourai_storage::actions::ActionExecutor;
use regex::Regex;
use std::collections::HashSet;

lazy_static! {
    static ref SLUR_REGEX: Regex = generalize_filters(SLURS);
    static ref DISCORD_INVITE_REGEX: Regex = Regex::new("discord.gg/([a-zA-Z0-9]+)").unwrap();
}

const SLURS: &[&str] = &[
    "nigger", "nigga", "tarskin", "tranny", "trannie", "redskin", "faggot",
    "chink", "kike", "dyke", "gook", "wigger"
];

pub async fn check_message(executor: &ActionExecutor, message: &impl MessageLike) -> Result<bool> {
    let guild_id = if let Some(guild_id) = message.guild_id() {
        guild_id
    } else {
        return Ok(false);
    };
    let mut storage = executor.storage().clone();
    let config: ModerationConfig = GuildConfig::fetch_or_default(guild_id, &mut storage).await?;

    for rule in config.get_message_filter().get_rules() {
        let reasons = get_filter_reasons(message, rule.get_criteria())?;
        if !reasons.is_empty() {
            apply_rule(message, rule, reasons, executor).await?;
            return Ok(rule.get_delete_message());
        }
    }

    Ok(false)
}

fn generalize_filters(filters: &[&str]) -> Regex {
    let joined = filters
        .iter()
        .map(|filter| generalize_filter(*filter))
        .collect::<Vec<String>>()
        .join("|");
    Regex::new(&format!("({})", joined)).unwrap()
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
    rule: &MessageFilterRule,
    reasons: Vec<String>,
    executor: &ActionExecutor,
) -> Result<()> {
    let mut action_taken = "";
    let guild_id = message.guild_id().unwrap();

    if rule.get_notify_moderator() {
        action_taken = "Message filter found notable message:";
    }

    if rule.get_delete_message() {
        action_taken = "Message filter deleted a message:";

        // Delete the message from the cache to avoid logging it when it gets deleted.
        CachedMessage::delete(message.channel_id(), message.id())
            .query_async(&mut executor.storage().clone())
            .await?;

        executor
            .http()
            .delete_message(message.channel_id(), message.id())
            .exec()
            .await?;
    }

    if !rule.additional_actions.is_empty() {
        for action_template in rule.additional_actions.iter() {
            let mut action = action_template.clone();
            action.set_guild_id(guild_id.get());
            action.set_user_id(message.author().id().get());
            executor.execute_action(&action).await?;
        }
    }

    if action_taken != "" {
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
                .exec()
                .await?;
        }
    }

    Ok(())
}

fn get_filter_reasons(
    message: &impl MessageLike,
    criteria: &MessageFilterRule_Criteria,
) -> Result<Vec<String>> {
    let mut reasons = Vec::new();
    for regex in criteria.matches.iter() {
        let regex = Regex::new(&regex)?;
        if regex.is_match(message.content()) {
            reasons.push(String::from("Message contains banned word or phrase."));
        }
    }

    if criteria.get_includes_slurs() {
        for word in message.content().split_whitespace() {
            if SLUR_REGEX.is_match(word) {
                reasons.push(
                    format!("Message contains recognized racial slur: {}", word)
                );
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
