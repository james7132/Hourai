pub mod cache;
pub mod config;
pub mod db;
pub mod error;
pub mod init;
pub mod prelude;

// Include the auto-generated protos as a module
pub mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
}

