use crate::{prelude::*, AppState};
use actix_web::{http::StatusCode, web};
use hourai::{models::id::GuildId, proto::auto_config::*, proto::guild_configs::*};
use hourai_redis::CachedGuildConfig;

async fn get_config<T>(
    data: web::Data<AppState>,
    path: web::Path<u64>,
) -> Result<Option<web::Json<T>>>
where
    T: protobuf::Message + CachedGuildConfig + serde::Serialize,
{
    // TODO(james7132): Properly set up authN and authZ for this
    if let Some(guild_id) = GuildId::new(path.into_inner()) {
        let proto  = data
            .redis
            .guild(guild_id)
            .configs()
            .fetch()
            .await
            .http_error(StatusCode::NOT_FOUND, "Guild not found")?;
        Ok(proto.map(web::Json))
    } else {
        http_error(StatusCode::NOT_FOUND, "Guild not found")
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
