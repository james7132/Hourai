use super::prelude::*;
use hourai::{
    models::{channel::message::allowed_mentions::AllowedMentions, id::ChannelId},
    proto::guild_configs::LoggingConfig,
};
use rand::Rng;
use hourai_redis::GuildConfig;

pub(super) async fn choose(ctx: &CommandContext) -> Result<Response> {
    let choices: Vec<&str> = ctx.all_strings("choice").collect();
    if choices.is_empty() {
        Ok(Response::ephemeral().content("Nothing to choose from!"))
    } else {
        let idx = rand::thread_rng().gen_range(0..choices.len());
        Ok(Response::direct().content(format!("I choose `{}`.", choices[idx])))
    }
}

pub(super) async fn pingmod(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    let config: LoggingConfig =
        GuildConfig::fetch_or_default(guild_id, &mut storage.clone()).await?;
    let (mention, ping) = hourai_storage::ping_online_mod(guild_id, storage).await?;

    let content = ctx
        .get_string("reason")
        .map(|reason| format!("{}: {}", ping, reason))
        .unwrap_or(ping);

    if config.has_modlog_channel_id() {
        ctx.http
            .create_message(ChannelId::new(config.get_modlog_channel_id()).unwrap())
            .content(&format!(
                "<@{}> used `/pingmod` to ping {} in <#{}>",
                ctx.user().id,
                mention,
                ctx.channel_id()
            ))?
            .allowed_mentions(AllowedMentions::builder().build())
            .exec()
            .await?;

        ctx.http
            .create_message(ctx.channel_id())
            .content(&content)?
            .exec()
            .await?;

        Ok(Response::ephemeral().content(format!("Pinged {} to this channel.", mention)))
    } else {
        Ok(Response::direct().content(&content))
    }
}
