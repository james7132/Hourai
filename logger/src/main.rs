mod config;
mod hourai;

use crate::hourai::Hourai;
use std::env;
use std::path::Path;
use std::path::PathBuf;

// Include the auto-generated protos as a module
mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

const DEFAULT_ENV: &'static str = "dev";
const CONFIG_DIR: &'static str = "/etc/hourai";

fn get_config_path() -> Box<Path> {
    let execution_env: String = match env::var("HOURAI_ENV") {
        Ok(val) => val,
        Err(_) => String::from(DEFAULT_ENV),
    }.to_lowercase();

    let mut buffer = PathBuf::new();
    buffer.push(CONFIG_DIR);
    buffer.set_file_name(execution_env);
    buffer.set_extension("json");
    return buffer.into_boxed_path();
}

#[tokio::main]
async fn main() {
    let config = config::load_config(get_config_path().as_ref());
    let mut hourai = Hourai::new(config).await;
    hourai.run().await;
}
