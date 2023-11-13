use super::prelude::*;
use hourai::{
    models::{
        channel::message::allowed_mentions::AllowedMentions, guild::scheduled_event::Status, id::Id,
    },
    proto::guild_configs::LoggingConfig,
};
use rand::Rng;

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
            ))?
            .allowed_mentions(Some(&AllowedMentions::builder().build()))
            .await?;

        ctx.http()
            .create_message(ctx.channel_id())
            .content(&content)?
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
