mod bot;
mod db;

use crate::config::HouraiConfig;

pub struct Hourai {
    //config: HouraiConfig,
    discord_client: bot::Client,
}

impl Hourai {
    /// Creates a instance of the Hourai Discord Bot. Panics if initialization fails
    pub async fn new(config: HouraiConfig) -> Self {
        Self {
            discord_client: bot::Client::new(&config)
                .await
                .expect("Failed to initialize Discord client"),
        }
    }

    pub async fn run(&mut self) {
        // TODO(james7132): Add web API to this.
        self.discord_client.run().await
    }
}
