use crate::{AppState, prelude::*};
use serde::{Serialize, Deserialize};
use time::Duration;
use cookie::SameSite;
use actix_web::{
    web, get, post, HttpRequest, HttpResponse, HttpMessage,
    http::Cookie,
};

const TOKEN_URL: &str = "https://discord.com/api/oauth2/token";
const COOKIE_KEY: &str = "discord_refresh_token";
const SCOPES: &str = "";

#[derive(Deserialize)]
struct TokenRequest {
    code: String,
}

#[derive(Serialize)]
struct DiscordTokenRequest<'a> {
    client_id: &'a str,
    client_secret: &'a str,
    redirect_uri: &'a str,
    code: &'a str,
    grant_type: &'a str,
}

#[derive(Serialize)]
struct DiscordRefreshRequest<'a> {
    client_id: &'a str,
    client_secret: &'a str,
    redirect_uri: &'a str,
    refresh_token: &'a str,
    grant_type: &'a str,
    scope: &'a str,
}

#[derive(Serialize, Deserialize)]
struct TokenResponse {
    access_token: String,
    // Do not forward the refresh token plainly to the client.
    #[serde(skip_serializing)]
    refresh_token: String,
    #[serde(skip_serializing_if="Option::is_none")]
    scope: Option<String>,
    #[serde(skip_serializing_if="Option::is_none")]
    expires_in: Option<u64>,
}

#[post("/token")]
async fn token(state: web::Data<AppState>, request: web::Json<TokenRequest>) -> WebResult<HttpResponse> {
    let body = serde_urlencoded::to_string(DiscordTokenRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        code: request.code.as_str(),
        grant_type: "authorization_code",
    }).unwrap();

    let response: TokenResponse = state.http.post(TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .send_body(body)
        .await?
        .json()
        .await?;

    // TODO(james7132): Store the refresh and access tokens in a database with explicit
    // expiration dates.
    let refresh_cookie = Cookie::build(COOKIE_KEY, response.refresh_token.as_str())
        .domain("hourai.gg")
        .path("/api/oauth/refresh")
        .max_age(Duration::days(7)) // 7 days
        .same_site(SameSite::Strict)
        .http_only(true)
        .secure(true)
        .finish();

    Ok(HttpResponse::Ok()
        .cookie(refresh_cookie)
        .json(response))
}

#[get("/refresh")]
async fn refresh(state: web::Data<AppState>, request: HttpRequest) -> WebResult<HttpResponse> {
    // TODO(james7132): Validate this against the refresh tokens stored locally to see if
    // they're valid.
    let refresh_token = match request.cookie(COOKIE_KEY) {
        Some(ref cookie) => cookie.value().to_owned(),
        None => return Err(WebError::UNAUTHORIZED),
    };

    let data = serde_urlencoded::to_string(DiscordRefreshRequest {
        client_id: state.config.discord.client_secret.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.client_secret.as_str(),
        refresh_token: refresh_token.as_str(),
        grant_type: "refresh_token",
        scope: SCOPES,
    }).unwrap();

    let response: TokenResponse  = state.http.post(TOKEN_URL)
        .header("Accept", "application/json")
        .header("Content-Type", "application/x-www-form-urlencoded")
        .send_body(data)
        .await?
        .json()
        .await?;

    Ok(HttpResponse::Ok().json(response))
}

#[get("/logout")]
async fn logout(request: HttpRequest) -> WebResult<HttpResponse> {
    // TODO(james7132): Remove token from database.
    if let Some(ref refresh_token) = request.cookie(COOKIE_KEY) {
        Ok(HttpResponse::NoContent()
            .del_cookie(refresh_token)
            .finish())
    } else {
        Err(WebError::UNAUTHORIZED)
    }
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(token);
    cfg.service(refresh);
    cfg.service(logout);
}
