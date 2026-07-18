use crate::config::HouraiConfig;
use metrics_exporter_prometheus::PrometheusBuilder;
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use tracing::debug;
use twilight_gateway::{ConfigBuilder, Intents, Shard, create_recommended};

pub fn start_logging() {
    tracing_subscriber::fmt()
        .with_level(true)
        .with_thread_ids(true)
        .with_timer(tracing_subscriber::fmt::time::UtcTime::rfc_3339())
        .with_env_filter(tracing_subscriber::filter::EnvFilter::from_default_env())
        .compact()
        .init();
}

pub fn init(config: &HouraiConfig) {
    start_logging();
    debug!("Loaded Config: {:?}", config);

    let metrics_port = config.metrics.port.unwrap_or(9090);
    let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(0, 0, 0, 0)), metrics_port);
    PrometheusBuilder::new()
        .with_http_listener(socket)
        .install()
        .expect("Failed to set up Prometheus metrics exporter");

    debug!(
        "Metrics endpoint listening on http://0.0.0.0:{}",
        metrics_port
    );
}

pub type GatewayShard = Shard;

pub async fn create_shards(
    config: &HouraiConfig,
    http: &twilight_http::Client,
    intents: Intents,
) -> anyhow::Result<Vec<GatewayShard>> {
    let token = config.discord.bot_token.clone();
    let gateway_config = ConfigBuilder::new(token, intents).build();
    let shards: Vec<_> = create_recommended(http, gateway_config, |_, builder| builder.build())
        .await?
        .collect();
    Ok(shards)
}

pub fn http_client(config: &HouraiConfig) -> twilight_http::Client {
    debug!("Creating Discord HTTP client");
    // Use the twilight HTTP proxy when configured
    if let Some(proxy) = config.discord.proxy.as_ref() {
        twilight_http::Client::builder()
            .token(config.discord.bot_token.clone())
            .proxy(proxy.clone(), true)
            .ratelimiter(None)
            .build()
    } else {
        twilight_http::Client::builder()
            .token(config.discord.bot_token.clone())
            .build()
    }
}
