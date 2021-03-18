use crate::Client;
use anyhow::Result;
use hourai::models::user::User;
use hourai::models::gateway::payload::{BanAdd, MemberRemove};
use hourai::models::id::*;
use hourai::proto::guild_configs::*;
use hourai_redis::GuildConfig;

async fn get_config(client: &mut Client, guild_id: GuildId) -> Result<Option<AnnouncementConfig>> {
    Ok(GuildConfig::fetch(guild_id, &mut client.redis).await?)
}

pub async fn on_member_join(mut client: Client, guild: GuildId, user: User) -> Result<()> {
    if let Some(config) = get_config(&mut client, guild).await? {
        // TODO(james7132): let this be customizable.
        let msg = format!("**{}** has joined the server.", user.name);
        broadcast(client, config.get_leaves(), msg);
    }
    Ok(())
}

pub async fn on_member_leave(mut client: Client, evt: MemberRemove) -> Result<()> {
    if let Some(config) = get_config(&mut client, evt.guild_id).await? {
        // TODO(james7132): let this be customizable.
        let msg = format!("**{}** has joined the server.", evt.user.name);
        broadcast(client, config.get_leaves(), msg);
    }
    Ok(())
}

pub async fn on_member_ban(mut client: Client, evt: BanAdd) -> Result<()> {
    if let Some(config) = get_config(&mut client, evt.guild_id).await? {
        // TODO(james7132): let this be customizable.
        let msg = format!("**{}** has been banned.", evt.user.name);
        broadcast(client, config.get_bans(), msg);
    }
    Ok(())
}

pub fn broadcast(
    client: Client,
    config: &AnnouncementTypeConfig,
    message: String,
) {
    async fn push(http: hourai::http::Client, channel: ChannelId, msg: String) -> Result<()> {
        http
          .create_message(channel)
          .content(msg)?
          .await?;
        Ok(())
    }

    for channel in config.get_channel_ids() {
        let http = client.http_client.clone();
        let channel_id = ChannelId(*channel);
        let msg = message.clone();
        tokio::spawn(async move {
            if let Err(err) = push(http, channel_id, msg).await {
                tracing::error!("Error while making announcment in {}: {}", channel_id, err);
            }
        });
    }
}

