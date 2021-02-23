#[deny(unused_must_use)]

mod config;
mod error;
mod hourai;

use crate::hourai::Hourai;
use tracing::debug;
use std::env;
use std::path::Path;
use std::path::PathBuf;

const DEFAULT_ENV: &'static str = "dev";

fn get_config_path() -> Box<Path> {
    let execution_env: String = match env::var("HOURAI_ENV") {
        Ok(val) => val,
        Err(_) => String::from(DEFAULT_ENV),
    }.to_lowercase();

    let mut buffer: PathBuf = ["/etc", "hourai"].iter().collect();
    buffer.push(execution_env);
    return buffer.into_boxed_path();
}

#[tokio::main]
async fn main() {
    tracing_subscriber::fmt()
        .with_level(true)
        .with_thread_ids(true)
        .with_timer(tracing_subscriber::fmt::time::ChronoUtc::rfc3339())
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .compact()
        .init();

    let config = config::load_config(get_config_path().as_ref());
    debug!("Loaded Config: {:?}", config);
    let mut hourai = Hourai::new(config).await;
    hourai.run().await;
}
