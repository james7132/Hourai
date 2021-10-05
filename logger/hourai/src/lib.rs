#[macro_use]
extern crate lazy_static;

pub mod cache;
pub mod commands;
pub mod config;
pub mod init;
pub mod interactions;
pub mod models;
pub mod prelude;
pub mod util;

// Include the auto-generated protos as a module
pub mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
    use self::cache::CachedRoleProto;
    use std::cmp::{Ord, Ordering, PartialOrd};

    impl Eq for CachedRoleProto {}

    impl Ord for CachedRoleProto {
        fn cmp(&self, other: &Self) -> Ordering {
            match self.get_position().cmp(&other.get_position()) {
                Ordering::Equal => self.get_role_id().cmp(&other.get_role_id()),
                ordering => ordering,
            }
        }
    }

    impl PartialOrd for CachedRoleProto {
        fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
            Some(self.cmp(other))
        }
    }
}

pub use twilight_gateway as gateway;
pub use twilight_http as http;
