#![allow(clippy::expect_used)]

use super::prelude::*;
use hourai::{
    models::{channel::message::AllowedMentions, guild::scheduled_event::Status, id::Id},
    proto::guild_configs::LoggingConfig,
};
use hourai_sql::Tag;
use rand::Rng;
use regex::Regex;
use std::sync::LazyLock;

static DICE_REGEX: LazyLock<Regex> =
    LazyLock::new(|| match Regex::new(r"(\d+)d(\d+)([\+\-\*\/]?)(\d*)") {
        Ok(re) => re,
        Err(_) => unreachable!(),
    });

pub(super) async fn choose(ctx: &CommandContext) -> Result<Response> {
    ctx.defer().await?;
    let choices: Vec<&str> = ctx.all_strings("choice").collect();
    if choices.is_empty() {
        Ok(Response::ephemeral().content("Nothing to choose from!"))
    } else {
        let idx = rand::thread_rng().gen_range(0..choices.len());
        Ok(Response::ephemeral().content(format!("I choose `{}`.", choices[idx])))
    }
}

pub(super) async fn roll(ctx: &CommandContext) -> Result<Response> {
    ctx.defer().await?;
    let input = ctx
        .get_string("dice")
        .ok()
        .cloned()
        .unwrap_or_else(|| "1d6".to_string());

    let caps = match DICE_REGEX.captures(&input) {
        Some(c) => c,
        None => {
            return Ok(
                Response::ephemeral().content("Invalid dice format. Use `3d6`, `1d20+5`, etc.")
            );
        }
    };

    let count: usize = caps[1].parse().unwrap_or(1).min(150);
    let sides: i64 = caps[2].parse().unwrap_or(6).max(1);
    let op = caps.get(3).map(|m| m.as_str()).unwrap_or("");
    let modifier: i64 = caps
        .get(4)
        .and_then(|m| m.as_str().parse().ok())
        .unwrap_or(0);

    let mut rolls = Vec::with_capacity(count);
    let mut rng = rand::thread_rng();
    for _ in 0..count {
        rolls.push(rng.gen_range(1..=sides));
    }

    let mut sum: i64 = rolls.iter().sum();
    match op {
        "+" => sum += modifier,
        "-" => sum -= modifier,
        "*" => sum *= modifier,
        "/" if modifier != 0 => sum /= modifier,
        _ => {}
    }

    rolls.sort();
    let roll_str = rolls
        .iter()
        .map(|r| r.to_string())
        .collect::<Vec<_>>()
        .join(", ");
    let resp = format!(
        "Rolled a total of `{}` from {} rolls:\n```\n{}\n```",
        sum, count, roll_str
    );
    Ok(Response::direct().content(resp))
}

pub(super) async fn ping_mod(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer_ephemeral().await?;
    let guild_id = ctx.guild_id()?;
    let config: LoggingConfig = storage.redis().guild(guild_id).configs().get().await?;
    let (mention, ping) = hourai_storage::ping_online_mod(guild_id, storage).await?;

    let content = ctx
        .get_string("reason")
        .map(|reason| format!("{}: {}", ping, reason))
        .unwrap_or(ping);

    if config.has_modlog_channel_id() {
        ctx.http()
            .create_message(Id::new(config.get_modlog_channel_id()))
            .content(&format!(
                "<@{}> used `/pingmod` to ping {} in <#{}>",
                ctx.user().id,
                mention,
                ctx.channel_id()
            ))
            .allowed_mentions(Some(&AllowedMentions::default()))
            .await?;

        ctx.http()
            .create_message(ctx.channel_id())
            .content(&content)
            .await?;

        Ok(Response::ephemeral().content(format!("Pinged {} to this channel.", mention)))
    } else {
        Ok(Response::direct().content(&content))
    }
}

pub(super) async fn ping_event(ctx: &CommandContext) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    let events = ctx
        .http
        .guild_scheduled_events(guild_id)
        .await?
        .model()
        .await?;

    let mut content = String::new();
    for event in events {
        if event.status != Status::Active || event.creator_id != Some(ctx.user().id) {
            continue;
        }
        let subscribers = ctx
            .http
            .guild_scheduled_event_users(guild_id, event.id)
            .await?
            .model()
            .await?;
        if subscribers.is_empty() {
            continue;
        }
        content.push_str(&event.name);
        content.push_str(": ");
        for subscriber in subscribers {
            content.push_str(&format!("<@{}> ", subscriber.user.id));
        }
        content.push('\n');
    }

    if content.is_empty() {
        Ok(Response::ephemeral().content("No events created by you are currently active."))
    } else {
        content.push('\n');
        content.push_str("We're starting!");
        Ok(Response::direct().content(&content))
    }
}

pub(super) async fn info_user(ctx: &CommandContext, executor: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let user_id = ctx.get_user("user")?;
    if let Ok(guild_id) = ctx.guild_id() {
        let member = executor
            .http()
            .guild_member(guild_id, user_id)
            .await?
            .model()
            .await?;
        let embed = hourai_sql::whois::member(executor.storage().sql(), &member).await?;
        return Ok(Response::direct().embed(embed.build()));
    }
    let user = executor.http().user(user_id).await?.model().await?;
    let embed = hourai_sql::whois::user(executor.storage().sql(), &user).await?;
    Ok(Response::direct().embed(embed.build()))
}

pub(super) async fn tag_get(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    let tag_name = ctx.get_string("name")?;

    let tag = Tag::get(guild_id, tag_name)
        .fetch_optional(storage.sql())
        .await?;

    match tag {
        Some(t) => Ok(Response::direct().content(t.response)),
        None => Ok(Response::ephemeral().content(format!("Tag `{}` does not exist.", tag_name))),
    }
}

pub(super) async fn tag_set(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    let tag_name = ctx.get_string("name")?;
    let content = ctx.get_string("response")?;

    Tag::set(guild_id, tag_name, content)
        .execute(storage.sql())
        .await?;

    Ok(Response::direct().content(format!("Tag `{}` set!", tag_name)))
}

pub(super) async fn tag_list(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;

    let tags: Vec<(String,)> = Tag::list(guild_id).fetch_all(storage.sql()).await?;

    if tags.is_empty() {
        Ok(Response::ephemeral().content("No tags have been set!"))
    } else {
        let list = tags
            .into_iter()
            .map(|(t,)| format!("`{}`", t))
            .collect::<Vec<_>>()
            .join(", ");
        Ok(Response::direct().content(format!("Available tags: {}", list)))
    }
}
