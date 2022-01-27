use crate::config::HouraiConfig;
use metrics_exporter_prometheus::PrometheusBuilder;
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::{convert::TryFrom, future::Future, pin::Pin, sync::Arc};
use tracing::debug;

pub fn start_logging() {
    tracing_subscriber::fmt()
        .with_level(true)
        .with_thread_ids(true)
        .with_timer(tracing_subscriber::fmt::time::ChronoUtc::rfc3339())
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .compact()
        .init();
}

pub fn init(config: &HouraiConfig) {
    start_logging();
    debug!("Loaded Config: {:?}", config);

    debug!("Loaded Config: {:?}", config);

    let metrics_port = config.metrics.port.unwrap_or(9090);
    let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(0, 0, 0, 0)), metrics_port);
    PrometheusBuilder::new()
        .listen_address(socket)
        .install()
        .expect("Failed to set up Prometheus metrics exporter");

    debug!(
        "Metrics endpoint listening on http://0.0.0.0:{}",
        metrics_port
    );
}

pub fn cluster(
    config: &HouraiConfig,
    intents: twilight_gateway::Intents,
) -> twilight_gateway::cluster::ClusterBuilder {
    let cluster = twilight_gateway::Cluster::builder(config.discord.bot_token.clone(), intents);
    if let Some(ref uri) = config.discord.gateway_queue {
        let queue = GatewayQueue(hyper::Uri::try_from(uri.clone()).unwrap());
        cluster.queue(Arc::new(queue))
    } else {
        cluster
    }
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

#[derive(Debug, Clone)]
pub struct GatewayQueue(hyper::Uri);

impl twilight_gateway::queue::Queue for GatewayQueue {
    fn request<'a>(&'a self, _: [u64; 2]) -> Pin<Box<dyn Future<Output = ()> + Send + 'a>> {
        tracing::debug!("Queueing to IDENTIFY with the gateway...");
        Box::pin(async move {
            if let Err(err) = hyper::Client::new().get(self.0.clone()).await {
                tracing::error!("Error while querying the shared gateway queue: {}", err);
            } else {
                tracing::debug!("Finished waiting to re-IDENTIFY with the Discord gateway.");
            }
        })
    }
}
