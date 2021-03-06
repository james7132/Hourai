mod client;

use hourai::{config};

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    client::run(config).await;
}
