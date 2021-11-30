use futures::future::Future;
use std::fmt::{Debug, Display};

pub async fn log_error<O, E: Display + Debug>(
    action: &'static str,
    fut: impl Future<Output = Result<O, E>>,
) {
    if let Err(err) = fut.await {
        tracing::error!("Error while {}: {} ({:?})", action, err, err);
    }
}

