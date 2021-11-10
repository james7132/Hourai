use super::prelude::*;
use hourai_storage::escalation::EscalationManager;

pub(super) async fn escalate(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    if !hourai_storage::is_moderator(
        guild_id,
        ctx.command.member.as_ref().unwrap().roles.iter().cloned(),
        &mut actions.storage().redis().clone(),
    )
    .await?
    {
        anyhow::bail!(CommandError::MissingPermission(
            "Only moderators can escalate users."
        ));
    }
    let authorizer = ctx.user();
    let reason = ctx.get_string("reason")?.as_ref();
    let amount = ctx.get_int("amount").unwrap_or(1);
    if amount <= 0 {
        anyhow::bail!(CommandError::InvalidArgument(
            "Non-positive `amounts` are not allowed. If you need to deescalate someone, please use \
             `/escalate down` instead.".to_owned()));
    }
    let manager = EscalationManager::new(actions.clone());
    let guild = manager.guild(guild_id).await?;
    let mut results = Vec::new();
    for user_id in ctx.all_users("user") {
        let history = guild.fetch_history(user_id).await?;
        let result = history
            .apply_delta(
                /*authorizer=*/ authorizer,
                /*reason=*/ reason,
                /*diff=*/ amount,
                /*execute=*/ amount >= 0,
            )
            .await;
        match result {
            Ok(escalation) => {
                results.push(format!(
                    "<@{}>: Action: {}. Expiration: {}",
                    user_id, escalation.entry.display_name, escalation.expiration()
                ));
            }
            Err(err) => {
                tracing::error!("Error while escalating a user: {}", err);
            }
        }
    }

    let response = format!(
        "Escalated {} users for: '{}'\n{}",
        results.len(),
        reason,
        results.join("\n")
    );
    Ok(Response::direct().content(response))
}

pub(super) async fn deescalate(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    let guild_id = ctx.guild_id()?;
    if !hourai_storage::is_moderator(
        guild_id,
        ctx.command.member.as_ref().unwrap().roles.iter().cloned(),
        &mut actions.storage().redis().clone(),
    )
    .await?
    {
        anyhow::bail!(CommandError::MissingPermission(
            "Only moderators can escalate users."
        ));
    }
    let authorizer = ctx.user();
    let reason = ctx.get_string("reason")?.as_ref();
    let amount = -ctx.get_int("amount").unwrap_or(1);
    if amount >= 0 {
        anyhow::bail!(CommandError::InvalidArgument(
            "Non-positive `amounts` are not allowed. If you need to deescalate someone, please use \
             `/escalate down` instead.".to_owned()));
    }
    let manager = EscalationManager::new(actions.clone());
    let guild = manager.guild(guild_id).await?;
    let mut results = Vec::new();
    for user_id in ctx.all_users("user") {
        let history = guild.fetch_history(user_id).await?;
        let result = history
            .apply_delta(
                /*authorizer=*/ authorizer,
                /*reason=*/ reason,
                /*diff=*/ amount,
                /*execute=*/ amount >= 0,
            )
            .await;
        match result {
            Ok(escalation) => {
                results.push(format!(
                    "<@{}>: Action: {}. Expiration {}",
                    user_id, escalation.entry.display_name, escalation.expiration()
                ));
            }
            Err(err) => {
                tracing::error!("Error while escalating a user: {}", err);
            }
        }
    }

    let response = format!(
        "Escalated {} users for: '{}'\n{}",
        results.len(),
        reason,
        results.join("\n")
    );
    Ok(Response::direct().content(response))
}

pub(super) async fn escalate_history(
    _ctx: &CommandContext,
    _actions: &ActionExecutor,
) -> Result<Response> {
    anyhow::bail!("This command is unfortunately not implemented yet.");
}
