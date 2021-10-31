use hourai::config::HouraiConfig;
use hourai::models::id::UserId;
use reqwest::RequestBuilder;
use serde_json::json;
use std::time::Duration;

pub async fn run_push_listings(client: crate::Client, config: HouraiConfig, interval: Duration) {
    let http = reqwest::Client::new();
    loop {
        let query = hourai_sql::Member::count_guilds()
            .fetch_one(client.sql())
            .await;
        let count = match query {
            Ok((cnt,)) => cnt,
            Err(err) => {
                tracing::error!("Error while fetching guild count for listings: {}", err);
                continue;
            }
        };
        if config.third_party.discord_bots_token.is_some() {
            let req = post_discord_bots(&http, client.user_id(), &config, count);
            tokio::spawn(handle_response("Discord Bots", req));
        }
        if config.third_party.discord_boats_token.is_some() {
            let req = post_discord_boats(&http, client.user_id(), &config, count);
            tokio::spawn(handle_response("Discord Boats", req));
        }
        if config.third_party.discord_boats_token.is_some() {
            let req = post_top_gg(&http, client.user_id(), &config, count);
            tokio::spawn(handle_response("top.gg", req));
        }
        tokio::time::sleep(interval).await;
    }
}

fn post_discord_bots(
    http: &reqwest::Client,
    user_id: UserId,
    config: &HouraiConfig,
    count: i64,
) -> RequestBuilder {
    let token = config
        .third_party
        .discord_bots_token
        .as_ref()
        .unwrap()
        .as_str();
    http.post(format!(
        "https://discord.bots.gg/api/v1/bots/{}/stats",
        user_id
    ))
    .header("Authorization", token)
    .json(&json!({ "guildCount": count }))
}

fn post_discord_boats(
    http: &reqwest::Client,
    user_id: UserId,
    config: &HouraiConfig,
    count: i64,
) -> RequestBuilder {
    let token = config
        .third_party
        .discord_boats_token
        .as_ref()
        .unwrap()
        .as_str();
    http.post(format!("https://discord.boats/api/bot/{}", user_id))
        .header("Authorization", token)
        .json(&json!({ "server_count": count }))
}

fn post_top_gg(
    http: &reqwest::Client,
    user_id: UserId,
    config: &HouraiConfig,
    count: i64,
) -> RequestBuilder {
    let token = config.third_party.top_gg_token.as_ref().unwrap().as_str();
    http.post(format!("https://top.gg/api/bots/{}/stats", user_id))
        .header("Authorization", token)
        .json(&json!({ "server_count": count }))
}

async fn handle_response(target: &str, request: RequestBuilder) {
    match request.send().await {
        Ok(response) => match response.status() {
            code if code.is_success() => {
                tracing::info!("Successfully bot info to {} ({})", target, code)
            }
            code if code.is_server_error() => {
                tracing::warn!("Failed to post bot info to {} ({})", target, code)
            }
            code if code.is_client_error() => {
                tracing::error!("Failed to post bot info to {} ({})", target, code)
            }
            _ => {}
        },
        Err(err) => tracing::error!("Error while posting bot info to {}: {}", target, err),
    }
}
