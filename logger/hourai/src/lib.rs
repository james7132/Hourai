#[macro_use]
extern crate lazy_static;

pub mod cache;
pub mod commands;
pub mod config;
pub mod db;
pub mod error;
pub mod init;
pub mod models;
pub mod prelude;
pub mod validation;

// Include the auto-generated protos as a module
pub mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

pub use twilight_gateway as gateway;
pub use twilight_http as http;
