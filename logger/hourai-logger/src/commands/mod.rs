mod admin;
mod escalation;
mod prelude;
mod standard;

use anyhow::Result;
use hourai::proto::action::StatusType;
use prelude::*;

pub async fn handle_command(ctx: CommandContext, actions: &ActionExecutor) -> Result<()> {
    let result = match ctx.command() {
        // Standard Commands
        Command::Command("choose") => standard::choose(&ctx).await,
        Command::Command("pingmod") => standard::pingmod(&ctx, actions.storage()).await,

        // Admin Commands
        Command::Command("ban") => admin::ban(&ctx, actions).await,
        Command::Command("kick") => admin::kick(&ctx, actions.storage()).await,
        Command::Command("mute") => admin::mute(&ctx, actions).await,
        Command::Command("deafen") => admin::deafen(&ctx, actions).await,
        Command::Command("move") => admin::move_cmd(&ctx, actions.storage()).await,
        Command::Command("prune") => admin::prune(&ctx).await,
        Command::SubCommand("role", "add") => {
            admin::change_role(&ctx, actions, StatusType::APPLY).await
        }
        Command::SubCommand("role", "remove") => {
            admin::change_role(&ctx, actions, StatusType::UNAPPLY).await
        }

        // Escalation commands
        Command::SubCommand("escalate", "up") => escalation::escalate(&ctx, actions).await,
        Command::SubCommand("escalate", "down") => escalation::deescalate(&ctx, actions).await,
        Command::SubCommand("escalate", "history") => {
            escalation::escalate_history(&ctx, actions).await
        }
        _ => return Err(anyhow::Error::new(CommandError::UnknownCommand)),
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
