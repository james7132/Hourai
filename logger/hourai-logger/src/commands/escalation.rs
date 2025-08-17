use super::prelude::*;
use hourai::models::id::{marker::UserMarker, Id};
use hourai_sql::{
    sql_types::chrono::{DateTime, Utc},
    EscalationEntry,
};
use hourai_storage::escalation::EscalationManager;
use tabled::{settings::Style, Table, Tabled};

pub(super) async fn escalate(ctx: &CommandContext, actions: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !hourai_storage::is_moderator(
        guild_id,
        ctx.command.member.as_ref().unwrap().roles.iter().cloned(),
        &mut actions.storage().redis().clone(),
    )
    .await?
    {
        anyhow::bail!(InteractionError::MissingPermission(
            "Only moderators can escalate users."
        ));
    }
    let authorizer = ctx.user();
    let reason = ctx.get_string("reason")?.as_ref();
    let amount = ctx.get_int("amount").unwrap_or(1);
    if amount <= 0 {
        anyhow::bail!(InteractionError::InvalidArgument(
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
                    user_id,
                    escalation.entry.display_name,
                    escalation.expiration()
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
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !hourai_storage::is_moderator(
        guild_id,
        ctx.command.member.as_ref().unwrap().roles.iter().cloned(),
        &mut actions.storage().redis().clone(),
    )
    .await?
    {
        anyhow::bail!(InteractionError::MissingPermission(
            "Only moderators can escalate users."
        ));
    }
    let authorizer = ctx.user();
    let reason = ctx.get_string("reason")?.as_ref();
    let amount = -ctx.get_int("amount").unwrap_or(1);
    if amount >= 0 {
        anyhow::bail!(InteractionError::InvalidArgument(
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
                    user_id,
                    escalation.entry.display_name,
                    escalation.expiration()
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
    ctx: &CommandContext,
    actions: &ActionExecutor,
) -> Result<Response> {
    async fn username_from_api(user_id: Id<UserMarker>, ctx: &CommandContext) -> Result<String> {
        let authorizer = ctx
            .http()
            .guild_member(ctx.guild_id()?, user_id)
            .exec()
            .await?
            .model()
            .await?;
        Ok(format!(
            "{}#{}",
            authorizer.user.name, authorizer.user.discriminator
        ))
    }

    ctx.defer().await?;

    let manager = EscalationManager::new(actions.clone());
    let guild = manager.guild(ctx.guild_id()?).await?;
    let user = ctx.get_user("user")?;
    let history = guild.fetch_history(user).await?;

    let mut level = 0;

    let history_response = if history.entries().is_empty() {
        "```\nNo history of escalation events.\n```".to_string()
    } else {
        #[derive(Debug, Tabled)]
        pub struct Row {
            date: String,
            action: String,
            authorizer: String,
            level: i32,
            reason: String,
        }

        let mut data = Vec::with_capacity(history.entries().len());
        for entry in history.entries() {
            level = (level + entry.level_delta).max(-1);
            let mut authorizer_name = username_from_api(Id::new(entry.authorizer_id as u64), ctx)
                .await
                .unwrap_or_else(|_| entry.authorizer_name.clone());

            let reason = entry
                .action
                .action
                .iter()
                .map(|action| action.get_reason().to_string())
                .collect::<Vec<_>>();
            let reason = reason.join("; ");

            data.push(Row {
                date: entry.timestamp.format("%b %d %Y %H:%M").to_string(),
                action: entry.display_name.clone(),
                authorizer: authorizer_name,
                level,
                reason,
            });
        }

        let mut table = Table::new(data);
        table.with(Style::markdown());
        format!("```\n{}\n```", table)
    };

    let username = username_from_api(user, ctx)
        .await
        .unwrap_or_else(|_| user.to_string());
    let response = format!(
        "**Escalation History for {}**\n{}",
        username, history_response
    );
    Ok(Response::direct().content(response))
}
