use crate::models::{SqlQuery, SqlQueryAs};
use crate::types;
use hourai::proto::action::Action;
use sqlx::types::chrono::{DateTime, Utc};

#[derive(Debug, sqlx::FromRow)]
pub struct PendingAction {
    id: i32,
    data: types::Protobuf<Action>,
}

impl PendingAction {
    pub fn fetch_expired<'a>() -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT id, data FROM pending_actions WHERE ts < now()")
    }

    pub fn schedule<'a>(action: Action, timestamp: impl Into<DateTime<Utc>>) -> SqlQuery<'a> {
        sqlx::query("INSERT INTO pending_actions (timestamp, data) VALUES ($1, $2)")
            .bind(timestamp.into())
            .bind(types::Protobuf(action))
    }

    pub fn delete<'a>(&self) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM pending_actions WHERE id = $1").bind(self.id)
    }
}
