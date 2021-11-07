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
                    tokio::spawn(run_action(executor.clone(), action));
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
    if let Err(err) = executor.execute_action(pending.action()).await {
        tracing::error!(
            "Error while running pending action ({:?}): {}",
            pending.action(),
            err
        );
    } else {
        executor.storage().sql().execute(pending.delete()).await?;
    }
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
                    tokio::spawn(run_deescalation(escalation_manager.clone(), deescalation));
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

    let deesc = history.apply_delta(
        /*authorizer=*/ user,
        /*reason=*/ "Automatic Deescalation",
        /*diff=*/ pending.amount,
        /*execute=*/ pending.amount >= 0,
    );

    if let Err(err) = deesc.await {
        tracing::error!(
            "Error while running pending deescalation (guild: {}, user: {}): {}",
            pending.guild_id(),
            pending.user_id(),
            err
        );
    } else {
        PendingDeescalation::delete(pending.guild_id(), pending.user_id())
            .execute(escalation_manager.executor().storage())
            .await?;
    }

    Ok(())
}
