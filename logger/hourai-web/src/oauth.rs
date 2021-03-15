use crate::AppState;
use actix_web::{
    web, get, post, HttpRequest, HttpResponse, HttpMessage, Responder,
};
use hyper::Uri;
use std::convert::TryFrom;

lazy_static! {
    static ref TOKEN_URL: Uri = Uri::try_from("https://discord.com/api/oauth2/token").unwrap();
}

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
    scope: &'a str,
    grant_type: &'a str,
}

#[post("/token")]
async fn token(state: web::Data<AppState>, request: web::Json<RefreshRequest>) -> impl Responder {
    let data = serde_urlencoded::to_string(DiscordTokenRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        code: request.code.as_str(),
        grant_type: "authorization_code",
    })?;

    let request = hyper::Request::post(*TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .body(data)?;

    let resp = state.hyper.request(request).await.await?;

    HttpResponse::Ok().finish()
}

#[get("/refresh")]
async fn refresh(state: web::Data<AppState>, request: HttpRequest) -> impl Responder {
    let token = match request.cookie(COOKIE_KEY) {
        Some(ref cookie) => cookie.value(),
        None => return HttpResponse::Unauthorized().finish(),
    };

    let data = serde_urlencoded::to_string(DiscordRefreshRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        refresh_token: token,
        grant_type: "refresh_token",
        scope: SCOPES,
    })?;

    let request = hyper::Request::post(*TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .body(data)?;

    let resp = state.hyper.request(request).await.await;

    HttpResponse::Ok().finish()
}

#[get("/logout")]
async fn logout(request: HttpRequest) -> impl Responder {
    if let Some(ref refresh_token) = request.cookie(COOKIE_KEY) {
        HttpResponse::NoContent()
            .del_cookie(refresh_token)
            .finish()
    } else {
        HttpResponse::Unauthorized()
            .finish()
    }
}
