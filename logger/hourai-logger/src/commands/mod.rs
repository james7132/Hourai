use anyhow::Result;
use thiserror::Error;

mod context;

pub use context::{Command, CommandContext, Response};
use tracing::error;

pub async fn handle_command(ctx: CommandContext) -> Result<()> {
    match ctx.command() {
        Command::Command("pingmod") => pingmod(ctx).await,
        cmd => {
            error!("Unknown command: {:?}", cmd);
            ctx.reply(Response::ephemeral().content("This is currently a placeholder. This command is currently unsuable.")).await?;
            anyhow::bail!(CommandError::UnknownCommand);
        }
    }
}

#[derive(Debug, Error)]
pub enum CommandError {
    #[error("Unkown Command")]
    UnknownCommand
}

async fn pingmod(ctx: CommandContext) -> Result<()> {
    ctx.reply(Response::ephemeral().content("Pinged <MODERATOR> to this channel.")).await?;
    Ok(())
}
