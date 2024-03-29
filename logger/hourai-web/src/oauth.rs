use crate::{prelude::*, AppState};
use actix_web::{
    body::BoxBody,
    cookie::{time::Duration, Cookie, SameSite},
    get,
    http::StatusCode,
    post, web, HttpRequest, HttpResponse,
};
use serde::{Deserialize, Serialize};

const TOKEN_URL: &str = "https://discord.com/api/oauth2/token";
const COOKIE_KEY: &str = "discord_refresh_token";
const SCOPES: &str = "guilds";

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
    #[serde(skip_serializing_if = "Option::is_none")]
    scope: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    expires_in: Option<i64>,
}

#[post("/token")]
async fn token(
    state: web::Data<AppState>,
    request: HttpRequest,
    token_request: web::Json<TokenRequest>,
) -> Result<HttpResponse> {
    let body = serde_urlencoded::to_string(DiscordTokenRequest {
        client_id: state.config.discord.client_id.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.redirect_uri.as_str(),
        code: token_request.code.as_str(),
        grant_type: "authorization_code",
    })
    .unwrap();

    let mut response = state
        .http
        .post(TOKEN_URL)
        .insert_header(("Accept", "application/json"))
        .insert_header(("Content-Type", "application/x-www-form-urlencoded"))
        .send_body(body)
        .await
        .http_internal_error("Failed to make a POST to Discord OAuth.")?;

    let data: TokenResponse = if response.status().is_success() {
        response
            .json()
            .await
            .http_internal_error("Failed to make a POST to Discord OAuth.")?
    } else {
        let body = BoxBody::new(response.body().await?);
        return Ok(HttpResponse::build(response.status()).body(body));
    };

    let host = request
        .headers()
        .get("Host")
        .and_then(|value| value.to_str().ok())
        .unwrap_or("hourai.gg");

    // TODO(james7132): Store the refresh and access tokens in a database with explicit
    // expiration dates.
    let refresh_cookie = Cookie::build(COOKIE_KEY, data.refresh_token.as_str())
        .domain(host)
        .path("/api/oauth/refresh")
        .max_age(Duration::days(7)) // 7 days
        .same_site(SameSite::Strict)
        .http_only(true)
        .secure(true)
        .finish();

    Ok(HttpResponse::Ok().cookie(refresh_cookie).json(data))
}

#[get("/refresh")]
async fn refresh(state: web::Data<AppState>, request: HttpRequest) -> Result<HttpResponse> {
    // TODO(james7132): Validate this against the refresh tokens stored locally to see if
    // they're valid.
    let refresh_token = request
        .cookie(COOKIE_KEY)
        .map(|cookie| cookie.value().to_owned())
        .http_error(StatusCode::UNAUTHORIZED, "Missing refresh token.")?;

    let data = serde_urlencoded::to_string(DiscordRefreshRequest {
        client_id: state.config.discord.client_id.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.redirect_uri.as_str(),
        refresh_token: refresh_token.as_str(),
        grant_type: "refresh_token",
        scope: SCOPES,
    })
    .unwrap();

    let mut response = state
        .http
        .post(TOKEN_URL)
        .insert_header(("Accept", "application/json"))
        .insert_header(("Content-Type", "application/x-www-form-urlencoded"))
        .send_body(data)
        .await
        .http_internal_error("Failed to refresh access token")?;

    let data: TokenResponse = if response.status().is_success() {
        response
            .json()
            .await
            .http_internal_error("Failed to refresh access_token.")?
    } else {
        let body = BoxBody::new(response.body().await?);
        return Ok(HttpResponse::build(response.status()).body(body));
    };

    Ok(HttpResponse::Ok().json(data))
}

#[post("/logout")]
async fn logout(request: HttpRequest) -> Result<HttpResponse> {
    // TODO(james7132): Remove token from database.
    if let Some(mut refresh_token) = request.cookie(COOKIE_KEY) {
        refresh_token.make_removal();
        Ok(HttpResponse::NoContent()
            .cookie(refresh_token.clone())
            .finish())
    } else {
        http_error(StatusCode::UNAUTHORIZED, "Missing login credentials.")
    }
}

pub fn scoped_config(cfg: &mut web::ServiceConfig) {
    cfg.service(token);
    cfg.service(refresh);
    cfg.service(logout);
}
