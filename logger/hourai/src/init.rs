use crate::config::HouraiConfig;
use tracing::debug;
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use metrics_exporter_prometheus::PrometheusBuilder;

pub fn init(config: &HouraiConfig) {
    tracing_subscriber::fmt()
        .with_level(true)
        .with_thread_ids(true)
        .with_timer(tracing_subscriber::fmt::time::ChronoUtc::rfc3339())
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .compact()
        .init();

    debug!("Loaded Config: {:?}", config);

    let metrics_port = config.metrics.port.unwrap_or(9090);
    let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), metrics_port);
    PrometheusBuilder::new()
        .listen_address(socket)
        .install()
        .expect("Failed to set up Prometheus metrics exporter");

    debug!("Exposed metrics on: {}", socket);
}

pub fn http_client(config: &HouraiConfig) -> twilight_http::Client {
    debug!("Creating Discord HTTP client");
    // Use the twilight HTTP proxy when configured
    if let Some(proxy) = config.discord.proxy.as_ref() {
        twilight_http::Client::builder()
            .token(&config.discord.bot_token)
            .proxy(proxy, true)
            .ratelimiter(None)
            .build()
    } else {
        twilight_http::Client::new(&config.discord.bot_token)
    }
}
