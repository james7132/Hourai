mod admin;
mod config;
mod escalation;
mod prelude;
mod standard;
mod verification;

use anyhow::Result;
use hourai::proto::action::StatusType;
use prelude::*;

pub async fn handle_command(ctx: CommandContext, actions: &ActionExecutor) -> Result<()> {
    let result = match ctx.command() {
        // Standard Commands
        Command::Command("choose") => standard::choose(&ctx).await,
        Command::Command("roll") => standard::roll(&ctx).await,
        Command::SubCommand("ping", "mod") => standard::ping_mod(&ctx, actions.storage()).await,
        Command::SubCommand("ping", "event") => standard::ping_event(&ctx).await,
        Command::SubCommand("info", "user") => standard::info_user(&ctx, actions).await,

        // Tag commands
        Command::SubCommand("tag", "get") => standard::tag_get(&ctx, actions.storage()).await,
        Command::SubCommand("tag", "set") => standard::tag_set(&ctx, actions.storage()).await,
        Command::SubCommand("tag", "list") => standard::tag_list(&ctx, actions.storage()).await,

        // Verification Commands
        Command::SubCommand("verification", "setup") => {
            verification::setup(&ctx, actions.storage()).await
        }
        Command::SubCommand("verification", "disable") => {
            verification::disable(&ctx, actions.storage()).await
        }
        Command::SubCommand("verification", "verify") => verification::verify(&ctx, actions).await,
        Command::SubCommand("verification", "purge") => verification::purge(&ctx, actions).await,
        Command::SubCommand("verification", "lockdown") => {
            verification::lockdown(&ctx, actions.storage()).await
        }
        Command::SubCommand("verification", "lockdown_lift") => {
            verification::lockdown_lift(&ctx, actions.storage()).await
        }
        Command::SubCommand("verification", "propagate") => {
            verification::propagate(&ctx, actions).await
        }

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
        Command::SubCommand("role", "allow") => admin::role_allow(&ctx, actions.storage()).await,
        Command::SubCommand("role", "forbid") => admin::role_forbid(&ctx, actions.storage()).await,
        Command::SubCommand("role", "get") => admin::role_get(&ctx, actions.storage()).await,
        Command::SubCommand("role", "drop") => admin::role_drop(&ctx, actions.storage()).await,

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

        Command::SubGroupCommand("config", "set", "dj") => {
            config::setdj(&ctx, actions.storage()).await
        }
        Command::SubGroupCommand("config", "set", "modlog") => {
            config::setmodlog(&ctx, actions.storage()).await
        }

        Command::SubGroupCommand("config", "announce", "join") => {
            config::announce_join(&ctx, actions.storage()).await
        }
        Command::SubGroupCommand("config", "announce", "leave") => {
            config::announce_leave(&ctx, actions.storage()).await
        }
        Command::SubGroupCommand("config", "announce", "ban") => {
            config::announce_ban(&ctx, actions.storage()).await
        }
        Command::SubGroupCommand("config", "announce", "voice") => {
            config::announce_voice(&ctx, actions.storage()).await
        }

        Command::SubGroupCommand("config", "log", "edited") => {
            config::log_edited(&ctx, actions.storage()).await
        }
        Command::SubGroupCommand("config", "log", "deleted") => {
            config::log_deleted(&ctx, actions.storage()).await
        }
        _ => return Ok(()),
    };

    tracing::info!("Received command: {:?}", ctx.command);

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(err) => {
            let response = Response::ephemeral();
            if let Some(command_err) = err.downcast_ref::<InteractionError>() {
                ctx.reply(response.content(format!(":x: Error: {}", command_err)))
                    .await?;
                Ok(())
            } else {
                ctx.reply(response.content(":x: Fatal Error: Internal Error has occured."))
                    .await?;
                Err(err)
            }
        }
    }
}
