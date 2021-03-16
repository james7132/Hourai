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
) -> Result<Option<web::Json<T>>, WebError> where T: protobuf::Message + CachedGuildConfig + serde::Serialize {
    // TODO(james7132): Properly set up authN and authZ for this
    let guild_id = GuildId(path.0);
    let mut redis = data.redis.clone();
    let response = GuildConfig::fetch::<T>(guild_id, &mut redis).await?
                               .map(|config| web::Json(config));
    Ok(response)
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(
        web::resource("/{guild_id}/announce")
            .route(web::get().to(get_config::<AnnouncementConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/auto")
            .route(web::get().to(get_config::<AutoConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/moderation")
            .route(web::get().to(get_config::<ModerationConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/logging")
            .route(web::get().to(get_config::<LoggingConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/music")
            .route(web::get().to(get_config::<MusicConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/role")
            .route(web::get().to(get_config::<RoleConfig>))
    );
    cfg.service(
        web::resource("/{guild_id}/validation")
            .route(web::get().to(get_config::<ValidationConfig>))
    );
}
