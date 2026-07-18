use super::{prelude::*, AppState};
use axum::{
    extract::{Path, State},
    http::StatusCode,
    routing::get,
    Json, Router,
};
use hourai::{models::id::Id, proto::auto_config::*, proto::guild_configs::*};
use hourai_redis::CachedGuildConfig;
use std::sync::Arc;

async fn get_config<T>(
    State(data): State<Arc<AppState>>,
    Path(guild_id): Path<u64>,
) -> Result<Json<Option<T>>>
where
    T: protobuf::Message + CachedGuildConfig + serde::Serialize,
{
    let proto = data
        .redis
        .guild(Id::new(guild_id))
        .configs()
        .fetch()
        .await
        .http_error(StatusCode::NOT_FOUND, "Guild not found")?;
    Ok(Json(proto))
}

pub fn router() -> Router<Arc<AppState>> {
    Router::new()
        .route("/:guild_id/announce", get(get_config::<AnnouncementConfig>))
        .route("/:guild_id/auto", get(get_config::<AutoConfig>))
        .route("/:guild_id/moderation", get(get_config::<ModerationConfig>))
        .route("/:guild_id/music", get(get_config::<MusicConfig>))
        .route("/:guild_id/logging", get(get_config::<LoggingConfig>))
        .route("/:guild_id/roles", get(get_config::<RoleConfig>))
        .route(
            "/:guild_id/validation",
            get(get_config::<VerificationConfig>),
        )
}
