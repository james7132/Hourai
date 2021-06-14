use serde::Deserialize;
use std::{
    env,
    fs::File,
    io::BufReader,
    path::{Path, PathBuf},
};
use twilight_model::application::command::Command;

const DEFAULT_ENV: &str = "dev";

#[derive(Debug, Deserialize, Clone)]
pub struct HouraiConfig {
    pub command_prefix: String,
    pub database: String,
    pub redis: String,
    pub music: MusicConfig,
    pub discord: DiscordConfig,
    pub commands: Vec<Command>,
    pub web: WebConfig,
    pub metrics: MetricsConfig,
    pub reddit: RedditConfig,
    pub third_party: ThirdPartyConfig,
}

#[derive(Debug, Deserialize, Clone)]
pub struct WebConfig {
    pub port: u16,
}

#[derive(Debug, Deserialize, Clone)]
pub struct MetricsConfig {
    pub port: Option<u16>,
}

#[derive(Debug, Deserialize, Clone)]
pub struct MusicConfig {
    pub nodes: Vec<MusicNode>,
}

#[derive(Debug, Deserialize, Clone)]
pub struct MusicNode {
    pub identifier: String,
    pub host: String,
    pub port: u16,
    pub rest_uri: String,
    pub region: String,
    pub password: String,
}

#[derive(Debug, Deserialize, Clone)]
pub struct DiscordConfig {
    pub application_id: u64,
    pub client_id: String,
    pub client_secret: String,
    pub redirect_uri: String,
    pub bot_token: String,
    pub proxy: Option<String>,
    pub gateway_queue: Option<String>,
}

#[derive(Debug, Deserialize, Clone)]
pub struct RedditConfig {
    pub client_id: String,
    pub client_secret: String,
    pub user_agent: String,
}

#[derive(Debug, Deserialize, Clone)]
pub struct ThirdPartyConfig {
    pub discord_boats_token: Option<String>,
    pub discord_bots_token: Option<String>,
    pub top_gg_token: Option<String>,
}

#[derive(Debug, Deserialize, Clone)]
pub struct WebhookConfig {
    pub bot_log: String,
}

/// Loads the config for the bot. Panics if the reading the files fails or parsing fails.
pub fn load_config(path: &Path) -> HouraiConfig {
    let file = File::open(path);
    assert!(file.is_ok(), "Cannot open JSON config at {:?}", path);
    let reader = BufReader::new(file.unwrap());
    simd_json::serde::from_reader(reader).unwrap()
}

pub fn get_config_path() -> Box<Path> {
    let mut buffer: PathBuf = ["/etc", "hourai"].iter().collect();
    let execution_env: String = env::var("HOURAI_ENV")
        .unwrap_or_else(|_| String::from(DEFAULT_ENV))
        .to_lowercase();
    buffer.push(execution_env);
    buffer.into_boxed_path()
}
