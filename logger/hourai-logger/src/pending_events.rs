use anyhow::Result;
use futures::future::Future;
use futures::stream::StreamExt;
use hourai_sql::{Executor, PendingAction, PendingDeescalation};
use hourai_storage::{actions::ActionExecutor, escalation::EscalationManager};
use std::fmt::{Debug, Display};
use tokio::time::{Duration, Instant};

const CYCLE_DURATION: Duration = Duration::from_secs(1);

async fn log_error<O, E: Display + Debug>(action: &'static str, fut: impl Future<Output = Result<O, E>>) {
    if let Err(err) = fut.await {
        tracing::error!("Error while {}: {} ({:?})", action, err, err);
    }
}

pub async fn run_pending_actions(executor: ActionExecutor) {
    loop {
        let next = Instant::now() + CYCLE_DURATION;
        let mut pending = PendingAction::fetch_expired().fetch(executor.storage().sql());
        while let Some(item) = pending.next().await {
            match item {
                Ok(action) => {
                    tokio::spawn(log_error(
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

async fn run_action(executor: ActionExecutor, pending: PendingAction) -> Result<()> {
    tracing::debug!("Running pending action: {:?}", pending.action());
    executor.execute_action(pending.action()).await?;
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
                    tokio::spawn(log_error(
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
