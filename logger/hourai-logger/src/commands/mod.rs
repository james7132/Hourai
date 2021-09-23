use anyhow::Result;
use hourai::{models::guild::Permissions, http::request::prelude::AuditLogReason};

mod context;

pub use context::{Command, CommandContext, CommandError, CommandResult, Response};

pub async fn handle_command(ctx: CommandContext) -> Result<()> {
    let result = match ctx.command() {
        Command::Command("pingmod") => pingmod(&ctx).await,

        // Admin Commands
        Command::Command("ban") => ban(&ctx).await,
        Command::Command("kick") => kick(&ctx).await,
        _ => Err(CommandError::UnknownCommand),
    };

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(CommandError::GenericError(err)) => Err(err),
        Err(err) => {
            ctx.reply(Response::ephemeral().content(format!(":x: Error: {}", err)))
                .await?;
            Ok(())
        }
    }
}

async fn pingmod(ctx: &CommandContext) -> CommandResult<Response> {
    if ctx.command.guild_id.is_none() {
        return Err(CommandError::NotInGuild);
    }

    Ok(Response::ephemeral().content("Pinged <MODERATOR> to this channel."))
}

async fn ban(ctx: &CommandContext) -> CommandResult<Response> {
    let guild_id = ctx.guild_id()?;
    let authorizer = ctx.command.user.as_ref().expect("Command without user.");
    let reason = if let Some(reason) = ctx.get_string("reason") {
        format!("Banned by {}#{} for: {}", authorizer.name, authorizer.discriminator, reason)
    } else {
        format!("Banned by {}#{}", authorizer.name, authorizer.discriminator)
    };

    let duration = ctx.get_string("duration");
    if duration.is_some() {
        return Err(CommandError::UserError("Temp bans via this command are currently not supported."));
    }
    let soft = ctx.get_flag("soft").unwrap_or(false);
    let users = ctx.all_user_options_named("user");
    let mut success = 0;
    let mut errors = Vec::new();

    // TODO(james7132): Properly display the errors.
    if soft {
        if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
            return Err(CommandError::MissingPermission("Kick Members"));
        }

        for user in users {
            let request = ctx.http.create_ban(guild_id, user.id)
                .delete_message_days(7)
                .unwrap()
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user.id, err));
                continue;
            }

            let request = ctx.http.delete_ban(guild_id, user.id)
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user.id, err));
            } else {
                success += 1;
            }
        }

        Ok(Response::direct().content(format!("Softbanned {} users.", success)))
    } else {
        if !ctx.has_user_permission(Permissions::BAN_MEMBERS) {
            return Err(CommandError::MissingPermission("Ban Members"));
        }

        for user in users {
            let request = ctx.http.create_ban(guild_id, user.id)
                .reason(&reason)
                .unwrap();
            if let Err(err) = request.exec().await {
                errors.push(format!("{}: {}", user.id, err));
            } else {
                success += 1;
            }
        }

        Ok(Response::direct().content(format!("Banned {} users.", success)))
    }
}

async fn kick(ctx: &CommandContext) -> CommandResult<Response> {
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
        return Err(CommandError::MissingPermission("Kick Members"));
    }

    let authorizer = ctx.command.user.as_ref().expect("Command without user.");
    let members = ctx.all_member_options_named("user");
    let mut success = 0;
    let mut errors = Vec::new();
    let reason = if let Some(reason) = ctx.get_string("reason") {
        format!("Banned by {}#{} for: {}", authorizer.name, authorizer.discriminator, reason)
    } else {
        format!("Banned by {}#{}", authorizer.name, authorizer.discriminator)
    };

    for member in members {
        let request = ctx.http.remove_guild_member(guild_id, member.id)
            .reason(&reason)
            .unwrap();
        if let Err(err) = request.exec().await {
            errors.push(format!("{}: {}", member.id, err));
        } else {
            success += 1;
        }
    }

    Ok(Response::direct().content(format!("Kicked {} users.", success)))
}
