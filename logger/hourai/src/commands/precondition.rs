use super::{CommandError, Context};
use crate::models::id::GuildId;
use anyhow::Result;

pub fn require_in_guild(ctx: &Context<'_>) -> Result<GuildId> {
    ctx.message
        .guild_id
        .ok_or_else(|| CommandError::FailedPrecondition("Command must be run in a server.").into())
}
