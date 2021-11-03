use anyhow::Result;
use hourai::interactions::{Command, CommandContext, CommandError, Response};
use hourai::{
    http::request::prelude::AuditLogReason,
    models::{
        guild::{Guild, Permissions},
        id::{ChannelId, UserId},
        user::User,
    },
    proto::guild_configs::LoggingConfig,
};
use hourai_redis::{CachedGuild, GuildConfig, RedisPool};
use hourai_sql::SqlPool;
use rand::Rng;

#[derive(Clone)]
pub struct StorageContext {
    pub redis: RedisPool,
    pub sql: SqlPool,
}

pub async fn handle_command(ctx: CommandContext, mut storage: StorageContext) -> Result<()> {
    let result = match ctx.command() {
        Command::Command("pingmod") => pingmod(&ctx, &mut storage).await,

        // Admin Commands
        Command::Command("ban") => ban(&ctx, &storage).await,
        Command::Command("kick") => kick(&ctx, &storage).await,
        _ => Err(anyhow::Error::new(CommandError::UnknownCommand)),
    };

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(err) => {
            let response = Response::ephemeral();
            if let Some(command_err) = err.downcast_ref::<CommandError>() {
                ctx.reply(response.content(format!(":x: Error: {}", command_err)))
                    .await?;
                Ok(())
            } else {
                // TODO(james7132): Add some form of tracing for this.
                ctx.reply(response.content(":x: Fatal Error: Internal Error has occured."))
                    .await?;
                Err(err)
            }
        }
    }
}

async fn pingmod(ctx: &CommandContext, storage: &mut StorageContext) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    let online_mods =
        hourai_storage::find_online_moderators(guild_id, &storage.sql, &mut storage.redis).await?;
    let guild = CachedGuild::fetch_resource::<Guild>(guild_id, guild_id, &mut storage.redis)
        .await?
        .ok_or(CommandError::NotInGuild)?;
    let config =
        GuildConfig::fetch_or_default::<LoggingConfig>(guild_id, &mut storage.redis).await?;

    let mention: String;
    let ping: String;
    if online_mods.is_empty() {
        mention = format!("<@{}>", guild.get_owner_id());
        ping = format!("<@{}>, No mods online!", guild.get_owner_id());
    } else {
        let idx = rand::thread_rng().gen_range(0..online_mods.len());
        mention = format!("<@{}>", online_mods[idx].user_id());
        ping = mention.clone();
    };

    if config.has_modlog_channel_id() {
        ctx.http
            .create_message(ChannelId::new(config.get_modlog_channel_id()).unwrap())
            .content(&format!(
                "<@{}> used `/pingmod` to ping <@{}> in <#{}>",
                ctx.command.user.as_ref().unwrap().id,
                mention,
                ctx.channel_id()
            ))?
            .exec()
            .await?;

        ctx.http
            .create_message(ctx.channel_id())
            .content(&ping)?
            .exec()
            .await?;

        Ok(Response::ephemeral().content(format!("Pinged <@{}> to this channel.", mention)))
    } else {
        Ok(Response::direct().content(&mention))
    }
}

fn build_reason(action: &str, authorizer: &User, reason: Option<&String>) -> String {
    if let Some(reason) = reason {
        format!(
            "{} by {}#{} for: {}",
            action, authorizer.name, authorizer.discriminator, reason
        )
    } else {
        format!(
            "{} by {}#{}",
            action, authorizer.name, authorizer.discriminator
        )
    }
}

async fn ban(ctx: &CommandContext, storage: &StorageContext) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    let mut redis = storage.redis.clone();
    let authorizer = ctx.command.member.as_ref().expect("Command without user.");
    let authorizer_roles = CachedGuild::role_set(guild_id, &authorizer.roles, &mut redis).await?;
    let reason = build_reason(
        "Banned",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason"),
    );

    let duration = ctx.get_string("duration");
    if duration.is_some() {
        anyhow::bail!(CommandError::UserError(
            "Temp bans via this command are currently not supported.",
        ));
    }
    let soft = ctx.get_flag("soft").unwrap_or(false);
    let users = ctx.all_id_options_named("user").filter_map(UserId::new);
    let mut success = 0;
    let mut errors = Vec::new();

    // TODO(james7132): Properly display the errors.
    if soft {
        if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
            anyhow::bail!(CommandError::MissingPermission("Kick Members"));
        }

        for user_id in users {
            if let Some(member) = ctx.resolve_member(user_id) {
                let roles = CachedGuild::role_set(guild_id, &member.roles, &mut redis).await?;
                if roles >= authorizer_roles {
                    errors.push(format!(
                        "{}: Has higher roles, not authorized to softban.",
                        user_id
                    ));
                    continue;
                }
            }

            let request = ctx
                .http
                .create_ban(guild_id, user_id)
                .delete_message_days(7)
                .unwrap()
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user_id, err));
                continue;
            }

            let request = ctx
                .http
                .delete_ban(guild_id, user_id)
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user_id, err));
            } else {
                success += 1;
            }
        }

        Ok(Response::direct().content(format!("Softbanned {} users.", success)))
    } else {
        if !ctx.has_user_permission(Permissions::BAN_MEMBERS) {
            anyhow::bail!(CommandError::MissingPermission("Ban Members"));
        }

        for user_id in users {
            if let Some(member) = ctx.resolve_member(user_id) {
                let roles = CachedGuild::role_set(guild_id, &member.roles, &mut redis).await?;
                if roles >= authorizer_roles {
                    errors.push(format!(
                        "{}: Has higher roles, not authorized to ban.",
                        user_id
                    ));
                    continue;
                }
            }

            let request = ctx
                .http
                .create_ban(guild_id, user_id)
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user_id, err));
            } else {
                success += 1;
            }
        }

        Ok(Response::direct().content(format!("Banned {} users.", success)))
    }
}

async fn kick(ctx: &CommandContext, storage: &StorageContext) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
        anyhow::bail!(CommandError::MissingPermission("Kick Members"));
    }

    let mut redis = storage.redis.clone();
    let authorizer = ctx.command.member.as_ref().expect("Command without user.");
    let authorizer_roles = CachedGuild::role_set(guild_id, &authorizer.roles, &mut redis).await?;
    let reason = build_reason(
        "Banned",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason"),
    );
    let members = ctx.all_id_options_named("user").filter_map(UserId::new);
    let mut success = 0;
    let mut errors = Vec::new();

    for member_id in members {
        if let Some(member) = ctx.resolve_member(member_id) {
            let roles = CachedGuild::role_set(guild_id, &member.roles, &mut redis).await?;
            if roles >= authorizer_roles {
                errors.push(format!(
                    "{}: Has higher or equal roles, not authorized to kick.",
                    member_id
                ));
                continue;
            }
        }

        let request = ctx
            .http
            .remove_guild_member(guild_id, member_id)
            .reason(&reason)
            .unwrap();
        if let Err(err) = request.exec().await {
            errors.push(format!("{}: {}", member_id, err));
        } else {
            success += 1;
        }
    }

    Ok(Response::direct().content(format!("Kicked {} users.", success)))
}
