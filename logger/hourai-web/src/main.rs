use actix_web::{get, App, HttpResponse, HttpServer, Responder};
use hourai::{config, init};

#[get("/api/v1/bot/status")]
async fn bot_status() -> impl Responder {
    HttpResponse::Ok().body("Placeholder.")
}

#[actix_web::main]
async fn main() -> std::io::Result<()> {
    let config = config::load_config(config::get_config_path().as_ref());
    init::init(&config);

    HttpServer::new(|| App::new().service(bot_status))
        .bind(format!("127.0.0.1:{}", config.web.port))?
        .run()
        .await
}
