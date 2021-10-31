use crate::{prelude::*, AppState};
use actix_web::{http::StatusCode, web};
use hourai::{models::id::GuildId, proto::auto_config::*, proto::guild_configs::*};
use hourai_redis::{CachedGuildConfig, GuildConfig};

async fn get_config<T>(
    data: web::Data<AppState>,
    path: web::Path<u64>,
) -> Result<Option<web::Json<T>>, WebError>
where
    T: protobuf::Message + CachedGuildConfig + serde::Serialize,
{
    // TODO(james7132): Properly set up authN and authZ for this
    if let Some(guild_id) = GuildId::new(path.into_inner()) {
        Ok(GuildConfig::fetch::<T>(guild_id, &mut data.redis.clone())
            .await
            .map_err(|_| WebError::GenericHTTPError(StatusCode::NOT_FOUND))?
            .map(web::Json))
    } else {
        Err(WebError::GenericHTTPError(StatusCode::NOT_FOUND))
    }
}

fn add_config<T: protobuf::Message + CachedGuildConfig + serde::Serialize>(
    cfg: &mut web::ServiceConfig,
    endpoint: &str,
) {
    cfg.service(
        web::resource(format!("/{{guild_id}}/{}", endpoint).as_str())
            .route(web::get().to(get_config::<T>)),
    );
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    add_config::<AnnouncementConfig>(cfg, "announce");
    add_config::<AutoConfig>(cfg, "auto");
    add_config::<ModerationConfig>(cfg, "moderation");
    add_config::<LoggingConfig>(cfg, "logging");
    add_config::<RoleConfig>(cfg, "roles");
    add_config::<VerificationConfig>(cfg, "validation");
}
