use hourai::config::HouraiConfig;
use hourai::models::id::ApplicationId;
use std::time::Duration;

type HttpClient = hyper::Client<hyper::client::connect::HttpConnector>;
type Request = hyper::Request<hyper::Body>;

pub async fn run_push_listings(client: crate::Client, config: HouraiConfig, interval: Duration) {
    let http = hyper::Client::new();
    loop {
        let count = client.cache.guilds().len();
        if config.third_party.discord_bots_token.is_some() {
            tokio::spawn(make_post(
                http.clone(),
                post_discord_bots(client.client_id, &config, count),
                "Discord Bots",
            ));
        }
        if config.third_party.discord_boats_token.is_some() {
            tokio::spawn(make_post(
                http.clone(),
                post_discord_boats(client.client_id, &config, count),
                "Discord Boats",
            ));
        }
        if config.third_party.discord_boats_token.is_some() {
            tokio::spawn(make_post(
                http.clone(),
                post_top_gg(client.client_id, &config, count),
                "top.gg",
            ));
        }
        tokio::time::sleep(interval).await;
    }
}

async fn make_post(client: HttpClient, request: Request, target: &str) {
    match client.request(request).await {
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

fn post_discord_bots(user_id: ApplicationId, config: &HouraiConfig, count: usize) -> Request {
    let token = config
        .third_party
        .discord_bots_token
        .as_ref()
        .unwrap()
        .as_str();
    let data = serde_json::json!({ "guildCount": count });
    let uri = format!("https://discord.bots.gg/api/v1/bot/{}/stats", user_id);
    let body = hyper::Body::from(serde_json::to_vec(&data).unwrap());
    hyper::Request::post(uri)
        .header("Authorization", token)
        .body(body)
        .unwrap()
}

fn post_discord_boats(user_id: ApplicationId, config: &HouraiConfig, count: usize) -> Request {
    let token = config
        .third_party
        .discord_boats_token
        .as_ref()
        .unwrap()
        .as_str();
    let data = serde_json::json!({ "server_count": count });
    let uri = format!("https://discord.boats/api/v2/bot/{}", user_id);
    let body = hyper::Body::from(serde_json::to_vec(&data).unwrap());
    hyper::Request::post(uri)
        .header("Authorization", token)
        .body(body)
        .unwrap()
}

fn post_top_gg(user_id: ApplicationId, config: &HouraiConfig, count: usize) -> Request {
    let token = config.third_party.top_gg_token.as_ref().unwrap().as_str();
    let data = serde_json::json!({ "server_count": count });
    let uri = format!("https://top.gg/api/v2/bot/{}", user_id);
    let body = hyper::Body::from(serde_json::to_vec(&data).unwrap());
    hyper::Request::post(uri)
        .header("Authorization", token)
        .body(body)
        .unwrap()
}
