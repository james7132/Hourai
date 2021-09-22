use anyhow::Result;
use hourai::models::guild::Permissions;
use thiserror::Error;

mod context;

pub use context::{Command, CommandContext, Response};
use tracing::error;

type CommandResult = std::result::Result<Response, CommandError>;

#[derive(Debug, Error)]
pub enum CommandError {
    #[error("Unkown command. This command is currently unsuable.")]
    UnknownCommand,
    #[error("Command can only be used in a server.")]
    NotInGuild,
    #[error("User is missing permission: `.0`")]
    MissingPermission(&'static str),
    #[error("Generic error: .0")]
    GenericError(#[from] anyhow::Error),
}

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

async fn pingmod(ctx: &CommandContext) -> CommandResult {
    if !ctx.command.guild_id.is_none() {
        return Err(CommandError::NotInGuild);
    }

    Ok(Response::ephemeral().content("Pinged <MODERATOR> to this channel."))
}

async fn ban(ctx: &CommandContext) -> CommandResult {
    if !ctx.command.guild_id.is_none() {
        return Err(CommandError::NotInGuild);
    }

    let soft = ctx.get_flag("soft").unwrap_or(false);

    if soft {
        if !ctx.has_user_permission(Permissions::BAN_MEMBERS) {
            return Err(CommandError::MissingPermission("Ban Members"));
        }

        let members = ctx.all_member_options_named("user");

        Ok(Response::direct().content(format!("Banned {} users.", members.count())))
    } else {
        if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
            return Err(CommandError::MissingPermission("Kick Members"));
        }

        let members = ctx.all_member_options_named("user");

        Ok(Response::direct().content(format!("Softbanned {} users.", members.count())))
    }
}

async fn kick(ctx: &CommandContext) -> CommandResult {
    if !ctx.command.guild_id.is_none() {
        return Err(CommandError::NotInGuild);
    }
    if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
        return Err(CommandError::MissingPermission("Kick Members"));
    }

    let members = ctx.all_member_options_named("user");

    Ok(Response::direct().content(format!("Kicked {} users.", members.count())))
}
