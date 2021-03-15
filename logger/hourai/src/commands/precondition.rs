use super::{CommandError, Context};
use crate::models::id::GuildId;
use anyhow::Result;
use twilight_command_parser::Arguments;

pub fn require_in_guild(ctx: &Context<'_>) -> Result<GuildId> {
    ctx.message
        .guild_id
        .ok_or_else(|| CommandError::FailedPrecondition("Command must be run in a server.").into())
}

pub fn no_excess_arguments(args: &mut Arguments) -> Result<()> {
    if let Some(_) = args.next() {
        return Err(CommandError::ExcessArguments.into());
    }
    Ok(())
}
