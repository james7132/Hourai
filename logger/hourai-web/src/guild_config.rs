use crate::AppState;
use hourai::{
    proto::auto_config::*,
    proto::guild_configs::*,
    models::id::GuildId
};
use hourai_redis::{GuildConfig, CachedGuildConfig};
use actix_web::{web, HttpResponse};
use tracing::error;

async fn config<T>(
    data: web::Data<AppState>,
    path: web::Path<u64>
) -> HttpResponse where T: protobuf::Message + CachedGuildConfig + serde::Serialize {
    // TODO(james7132): Properly set up authN and authZ for this
    match GuildConfig::fetch::<T>(GuildId(path.0), &mut data.redis).await {
        Ok(config) => HttpResponse::Ok().json(config),
        Err(err) => {
            error!("Error while fetching guild config: {}", err);
            HttpResponse::InternalServerError().finish()
        }
    }
}
