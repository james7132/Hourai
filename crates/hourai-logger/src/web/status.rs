use super::{prelude::*, AppState};
use axum::{extract::State, Json};
use serde::Serialize;
use std::sync::Arc;

#[derive(Serialize)]
pub struct BotStatus {
    pub shards: Vec<ShardStatus>,
}

#[derive(Serialize)]
pub struct ShardStatus {
    pub shard_id: u16,
    pub guilds: i64,
    pub members: i64,
}

pub async fn bot_status(State(data): State<Arc<AppState>>) -> Result<Json<BotStatus>> {
    let guilds = hourai_sql::Member::count_guilds()
        .fetch_one(&data.sql)
        .await
        .http_internal_error("Failed to fetch guild count")?
        .0;
    let members = hourai_sql::Member::count_members()
        .fetch_one(&data.sql)
        .await
        .http_internal_error("Failed to fetch member count")?
        .0;
    Ok(Json(BotStatus {
        shards: vec![ShardStatus {
            shard_id: 0,
            guilds,
            members,
        }],
    }))
}
