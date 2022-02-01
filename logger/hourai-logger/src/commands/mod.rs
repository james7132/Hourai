mod admin;
mod config;
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
        Command::SubCommand("info", "user") => standard::info_user(&ctx, actions).await,

        // Admin Commands
        Command::Command("ban") => admin::ban(&ctx, actions).await,
        Command::Command("kick") => admin::kick(&ctx, actions.storage()).await,
        Command::Command("timeout") => admin::timeout(&ctx, actions.storage()).await,
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
        // Config commands
        Command::SubGroupCommand("config", "reddit", "add") => config::reddit_add(&ctx).await,
        Command::SubGroupCommand("config", "reddit", "remove") => config::reddit_remove(&ctx).await,
        Command::SubGroupCommand("config", "reddit", "list") => config::reddit_list(&ctx).await,

        Command::SubGroupCommand("config", "set", "dj") => config::setdj(&ctx).await,
        Command::SubGroupCommand("config", "set", "modlog") => config::setmodlog(&ctx).await,

        Command::SubGroupCommand("config", "announce", "join") => config::announce_join(&ctx).await,
        Command::SubGroupCommand("config", "announce", "leave") => {
            config::announce_leave(&ctx).await
        }
        Command::SubGroupCommand("config", "announce", "ban") => config::announce_ban(&ctx).await,
        Command::SubGroupCommand("config", "announce", "voice") => {
            config::announce_voice(&ctx).await
        }

        Command::SubGroupCommand("config", "log", "edited") => config::log_edited(&ctx).await,
        Command::SubGroupCommand("config", "log", "deleted") => config::log_deleted(&ctx).await,
        _ => return Ok(()),
    };

    tracing::info!("Recieved command: {:?}", ctx.command);

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(err) => {
            let response = Response::ephemeral();
            if let Some(command_err) = err.downcast_ref::<InteractionError>() {
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
