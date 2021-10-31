mod models;
mod reddit;

use futures::channel::mpsc::UnboundedSender;
use futures::prelude::*;
use hourai::{config, init};
use hourai_sql::SqlPool;
use std::sync::Arc;

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    init::init(&config);

    let (tx, mut rx) = futures::channel::mpsc::unbounded();
    let client = Client {
        http: Arc::new(init::http_client(&config)),
        sql: hourai_sql::init(&config).await,
        tx,
    };

    tokio::spawn(reddit::start(client.clone(), config.reddit));

    while let Some(post) = rx.next().await {
        tracing::info!("New post: {:?}", post);
        if let Err(err) = post.broadcast(client.clone()) {
            tracing::error!("Error while broadcasting post to Discord: {}", err);
        }
    }
}

#[derive(Clone)]
pub struct Client {
    pub http: Arc<hourai::http::Client>,
    pub sql: SqlPool,
    tx: UnboundedSender<models::Post>,
}
