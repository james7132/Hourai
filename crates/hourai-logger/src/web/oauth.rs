use super::{prelude::*, AppState};
use axum::{
    extract::State,
    http::{HeaderMap, StatusCode},
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use std::sync::Arc;
use tower_cookies::{cookie::SameSite, Cookie, Cookies};

const TOKEN_URL: &str = "https://discord.com/api/oauth2/token";
const COOKIE_KEY: &str = "discord_refresh_token";
const SCOPES: &str = "guilds";

#[derive(Deserialize)]
pub struct TokenRequest {
    pub code: String,
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
pub struct TokenResponse {
    pub access_token: String,
    // Do not forward the refresh token plainly to the client.
    #[serde(skip_serializing)]
    pub refresh_token: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub scope: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub expires_in: Option<i64>,
}

async fn token(
    State(state): State<Arc<AppState>>,
    cookies: Cookies,
    headers: HeaderMap,
    Json(token_request): Json<TokenRequest>,
) -> Result<Json<TokenResponse>> {
    let form = DiscordTokenRequest {
        client_id: state.config.discord.client_id.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.redirect_uri.as_str(),
        code: token_request.code.as_str(),
        grant_type: "authorization_code",
    };

    let response = state
        .http
        .post(TOKEN_URL)
        .header("Accept", "application/json")
        .form(&form)
        .send()
        .await
        .http_internal_error("Failed to make a POST to Discord OAuth.")?;

    let status = response.status();
    if !status.is_success() {
        let code = StatusCode::from_u16(status.as_u16()).unwrap_or(StatusCode::BAD_GATEWAY);
        return http_error(code, "Discord OAuth token request failed.");
    }

    let data: TokenResponse = response
        .json()
        .await
        .http_internal_error("Failed to parse Discord OAuth token response.")?;

    let host = headers
        .get("Host")
        .and_then(|value| value.to_str().ok())
        .unwrap_or("hourai.gg");

    let mut cookie = Cookie::new(COOKIE_KEY, data.refresh_token.clone());
    cookie.set_domain(host.to_string());
    cookie.set_path("/api/oauth/refresh");
    cookie.set_same_site(SameSite::Strict);
    cookie.set_http_only(true);
    cookie.set_secure(true);
    cookies.add(cookie);

    Ok(Json(data))
}

async fn refresh(
    State(state): State<Arc<AppState>>,
    cookies: Cookies,
) -> Result<Json<TokenResponse>> {
    let refresh_token = cookies
        .get(COOKIE_KEY)
        .map(|cookie| cookie.value().to_owned())
        .http_error(StatusCode::UNAUTHORIZED, "Missing refresh token.")?;

    let form = DiscordRefreshRequest {
        client_id: state.config.discord.client_id.as_str(),
        client_secret: state.config.discord.client_secret.as_str(),
        redirect_uri: state.config.discord.redirect_uri.as_str(),
        refresh_token: refresh_token.as_str(),
        grant_type: "refresh_token",
        scope: SCOPES,
    };

    let response = state
        .http
        .post(TOKEN_URL)
        .header("Accept", "application/json")
        .form(&form)
        .send()
        .await
        .http_internal_error("Failed to refresh access token")?;

    let status = response.status();
    if !status.is_success() {
        let code = StatusCode::from_u16(status.as_u16()).unwrap_or(StatusCode::BAD_GATEWAY);
        return http_error(code, "Discord OAuth refresh request failed.");
    }

    let data: TokenResponse = response
        .json()
        .await
        .http_internal_error("Failed to parse Discord OAuth refresh response.")?;

    Ok(Json(data))
}

async fn logout(cookies: Cookies) -> Result<StatusCode> {
    if cookies.get(COOKIE_KEY).is_some() {
        cookies.remove(Cookie::new(COOKIE_KEY, ""));
        Ok(StatusCode::NO_CONTENT)
    } else {
        http_error(StatusCode::UNAUTHORIZED, "Missing login credentials.")
    }
}

pub fn router() -> Router<Arc<AppState>> {
    Router::new()
        .route("/token", post(token))
        .route("/refresh", get(refresh))
        .route("/logout", post(logout))
}
