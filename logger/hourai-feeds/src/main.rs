mod models;
mod reddit;

use futures::channel::mpsc::UnboundedSender;
use futures::prelude::*;
use hourai::{config, init};
use hourai_sql::SqlPool;

#[tokio::main]
async fn main() {
    //let config = config::load_config(config::get_config_path().as_ref());
    //init::init(&config);
    init::start_logging();

    let (tx, mut rx) = futures::channel::mpsc::unbounded();
    let client = Client {
        //http: init::http_client(&config),
        //sql: hourai_sql::init(&config).await,
        tx,
    };

    tokio::spawn(reddit::start(client.clone()));

    while let Some(post) = rx.next().await {
        println!("New post: {:?}", post)
        //post.broadcast(client.clone());
    }
}

#[derive(Clone)]
pub struct Client {
    //http: hourai::http::Client,
    //sql: SqlPool,
    tx: UnboundedSender<models::Post>,
}
