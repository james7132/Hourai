use anyhow::Result;
use hourai::proto::{action::Action, guild_configs::*};
use hourai::{
    models::{message::MessageLike, Snowflake},
    util::mentions,
};
use hourai_redis::GuildConfig;
use hourai_redis::RedisPool;
use regex::Regex;
use std::collections::HashSet;

pub async fn check_message(redis: &mut RedisPool, message: &impl MessageLike) -> Result<()> {
    let guild_id = if let Some(guild_id) = message.guild_id() {
        guild_id
    } else {
        return Ok(());
    };
    let config = GuildConfig::fetch_or_default::<ModerationConfig>(guild_id, redis).await?;

    for rule in config.get_message_filter().get_rules() {
        let reasons = get_filter_reasons(message, rule.get_criteria())?;
        if !reasons.is_empty() {
            apply_rule(message, rule, reasons).await?;
            break;
        }
    }

    Ok(())
}

async fn apply_rule(
    message: &impl MessageLike,
    rule: &MessageFilterRule,
    reasons: Vec<String>,
) -> Result<()> {
    //tasks = []
    let mut action_taken = "";
    let mention_mod = rule.get_notify_moderator();
    let reasons_block = format!("\n```\n{}\n```", reasons.join("\n"));
    let guild_id = message.guild_id().unwrap();

    if rule.get_notify_moderator() {
        action_taken = "Message filter found notable message:";
    }

    if rule.get_delete_message() {}

    if !rule.additional_actions.is_empty() {
        let mut actions = Vec::new();
        for action_template in rule.additional_actions.iter() {
            let mut action = action_template.clone();
            action.set_guild_id(guild_id.get());
            action.set_user_id(message.author().id().get());
            actions.push(action);
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
            //if SLUR_FILTER.is_match(word):
            //reasons.push(
            //format!("Message contains recognized racial slur: {}", word));
            //break;
        }
    }

    if criteria.get_includes_invite_links() {
        //if regex.is_match(message.content()) {
        //reasons.push(String::from("Message contains Discord invite link."));
        //}
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
