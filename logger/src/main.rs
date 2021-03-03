#[deny(unused_must_use)]

mod cache;
mod config;
mod db;
mod error;
mod init;
mod prelude;

#[cfg(feature="logger")]
mod logger;

#[cfg(feature="music")]
mod music;

// Include the auto-generated protos as a module
mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

use std::env;
use std::path::Path;
use std::path::PathBuf;
use tracing::{debug, info};

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
    let initializer = init::Initializer::new(config);

    #[cfg(feature="logger")]
    info!("Enabled module: logger");

    #[cfg(feature="music")]
    info!("Enabled module: music");

    futures::join!(
        #[cfg(feature="logger")]
        logger::run(initializer.clone()),
        #[cfg(feature="music")]
        music::run(initializer.clone()),
    );
}
