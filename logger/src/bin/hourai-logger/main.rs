mod client;

use hourai::{config, init};

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());
    let initializer = init::Initializer::new(config);
    client::run(initializer).await;
}
