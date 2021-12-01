use crate::utils;
use anyhow::Result;
use futures::stream::StreamExt;
use hourai_sql::{Executor, PendingAction, PendingDeescalation};
use hourai_storage::{actions::ActionExecutor, escalation::EscalationManager};
use tokio::time::{Duration, Instant};

const CYCLE_DURATION: Duration = Duration::from_secs(1);

pub async fn run_pending_actions(executor: ActionExecutor) {
    loop {
        let next = Instant::now() + CYCLE_DURATION;
        let mut pending = PendingAction::fetch_expired().fetch(executor.storage().sql());
        while let Some(item) = pending.next().await {
            match item {
                Ok(action) => {
                    tokio::spawn(utils::log_error(
                        "running pending action",
                        run_action(executor.clone(), action),
                    ));
                }
                Err(err) => {
                    tracing::error!("Error while fetching pending actions: {}", err);
                    break;
                }
            }
        }
        tokio::time::sleep_until(next).await;
    }
}

fn is_client_error(err: &anyhow::Error) -> bool {
    use hourai::http::error::*;
    if let Some(err) = err.downcast_ref::<Error>() {
        if let ErrorType::Response { status, .. } = err.kind() {
            return status.is_client_error();
        }
    }
    return false;
}

async fn run_action(executor: ActionExecutor, pending: PendingAction) -> Result<()> {
    tracing::debug!("Running pending action: {:?}", pending.action());
    if let Err(err) = executor.execute_action(pending.action()).await {
        if !is_client_error(&err) {
            return Err(err);
        } else {
            tracing::error!("Client Error while running pending actions: {}", err);
        }
    }
    executor.storage().sql().execute(pending.delete()).await?;
    tracing::info!("Ran pending action: {:?}", pending.action());
    Ok(())
}

pub async fn run_pending_deescalations(executor: ActionExecutor) {
    let storage = executor.storage().clone();
    let escalation_manager = EscalationManager::new(executor);
    loop {
        let next = Instant::now() + CYCLE_DURATION;
        let mut pending = PendingDeescalation::fetch_expired().fetch(&storage);
        while let Some(item) = pending.next().await {
            match item {
                Ok(deescalation) => {
                    tokio::spawn(utils::log_error(
                        "running automatic deescalation",
                        run_deescalation(escalation_manager.clone(), deescalation),
                    ));
                }
                Err(err) => {
                    tracing::error!("Error while fetching pending deescalations: {}", err);
                    break;
                }
            }
        }
        tokio::time::sleep_until(next).await;
    }
}

async fn run_deescalation(
    escalation_manager: EscalationManager,
    pending: PendingDeescalation,
) -> Result<()> {
    let user = escalation_manager.executor().current_user();
    let guild = escalation_manager.guild(pending.guild_id()).await?;
    let history = guild.fetch_history(pending.user_id()).await?;

    history
        .apply_delta(
            /*authorizer=*/ user,
            /*reason=*/ "Automatic Deescalation",
            /*diff=*/ pending.amount,
            /*execute=*/ pending.amount >= 0,
        )
        .await?;

    tracing::info!(
        "Ran automatic deescalation for user {} in guild {}",
        pending.guild_id(),
        pending.user_id(),
    );

    Ok(())
}
