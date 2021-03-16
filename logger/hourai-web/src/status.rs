use crate::prelude::*;
use crate::AppState;
use serde::Serialize;
use actix_web::{web, get};

#[derive(Serialize)]
struct BotStatus {
    shards: Vec<ShardStatus>
}

#[derive(Serialize)]
struct ShardStatus {
    shard_id: u16,
    guilds: i64,
    members: i64
}

#[get("/status")]
async fn bot_status(data: web::Data<AppState>) -> JsonResult<BotStatus> {
    Ok(web::Json(BotStatus {
        shards: vec![ShardStatus {
            shard_id: 0,
            guilds: hourai_sql::Member::count_guilds().fetch_one(&data.sql).await?.0,
            members: hourai_sql::Member::count_members().fetch_one(&data.sql).await?.0,
        }]
    }))
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(bot_status);
}
