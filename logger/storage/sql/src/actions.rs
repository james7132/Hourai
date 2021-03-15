use crate::models::{SqlQuery, SqlQueryAs};
use crate::types;
use hourai::proto::action::Action;

#[derive(Debug, sqlx::FromRow)]
pub struct PendingAction {
    id: i32,
    data: types::Protobuf<Action>,
}

impl PendingAction {
    pub fn fetch_expired<'a>() -> SqlQueryAs<'a, Self> {
        let now = types::UnixTimestamp::now();
        sqlx::query_as("SELECT id, data FROM pending_actions WHERE timestamp < $1").bind(now.0)
    }

    pub fn schedule<'a>(
        action: Action,
        timestamp: impl Into<types::UnixTimestamp>,
    ) -> SqlQuery<'a> {
        sqlx::query("INSERT INTO pending_actions (timestamp, data) VALUES ($1, $2)")
            .bind(timestamp.into().0)
            .bind(types::Protobuf(action))
    }

    pub fn delete<'a>(&self) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM pending_actions WHERE id = $1").bind(self.id)
    }
}
