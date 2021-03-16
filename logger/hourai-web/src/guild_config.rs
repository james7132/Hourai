use crate::{AppState, prelude::*};
use hourai::{
    proto::auto_config::*,
    proto::guild_configs::*,
    models::id::GuildId
};
use hourai_redis::{GuildConfig, CachedGuildConfig};
use actix_web::web;

async fn get_config<T>(
    data: web::Data<AppState>,
    path: web::Path<u64>
) -> JsonResult<T> where T: protobuf::Message + CachedGuildConfig + serde::Serialize {
    // TODO(james7132): Properly set up authN and authZ for this
    let guild_id = GuildId(path.0);
    let mut redis = data.redis.clone();
    Ok(web::Json(GuildConfig::fetch::<T>(guild_id, &mut redis).await?))
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(
        web::resource("/{guild_id}/auto")
            .route(web::get().to(get_config::<AutoConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/moderation")
            .route(web::get().to(get_config::<ModerationConfig>))
    );
}
