use crate::AppState;
use actix_web::{
    web, get, post, HttpRequest, HttpResponse, HttpMessage, Responder,
};

const TOKEN_URL: &str = "https://discord.com/api/oauth2/token";
const COOKIE_KEY: &str = "discord_refresh_token";
const SCOPES: &str = "";

#[derive(serde::Deserialize)]
struct RefreshRequest {
    code: String,
}

#[derive(serde::Serialize)]
struct DiscordTokenRequest<'a> {
    client_id: &'a str,
    client_secret: &'a str,
    redirect_uri: &'a str,
    code: &'a str,
    grant_type: &'a str,
}

#[derive(serde::Serialize)]
struct DiscordRefreshRequest<'a> {
    client_id: &'a str,
    client_secret: &'a str,
    redirect_uri: &'a str,
    refresh_token: &'a str,
    grant_type: &'a str,
    scope: &'a str,
}

#[post("/token")]
async fn token(state: web::Data<AppState>, request: web::Json<RefreshRequest>) -> HttpResponse {
    let body = serde_urlencoded::to_string(DiscordTokenRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        code: request.code.as_str(),
        grant_type: "authorization_code",
    }).unwrap();

    let response = state.http.post(TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .send_body(body)
        .await;

    HttpResponse::Ok().finish()
}

#[get("/refresh")]
async fn refresh(state: web::Data<AppState>, request: HttpRequest) -> HttpResponse {
    // TODO(james7132): Validate this against the refresh tokens stored locally to see if
    // they're valid.

    let data = serde_urlencoded::to_string(DiscordRefreshRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        refresh_token: match request.cookie(COOKIE_KEY) {
            Some(ref cookie) => cookie.value(),
            None => return HttpResponse::Unauthorized().finish(),
        },
        grant_type: "refresh_token",
        scope: SCOPES,
    }).unwrap();

    let request = state.http.post(TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .send_body(data)
        .await;

    HttpResponse::Ok().finish()
}

#[get("/logout")]
async fn logout(request: HttpRequest) -> impl Responder {
    // TODO(james7132): Remove token from database.
    if let Some(ref refresh_token) = request.cookie(COOKIE_KEY) {
        HttpResponse::NoContent()
            .del_cookie(refresh_token)
            .finish()
    } else {
        HttpResponse::Unauthorized()
            .finish()
    }
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(token);
    cfg.service(refresh);
    cfg.service(logout);
}
