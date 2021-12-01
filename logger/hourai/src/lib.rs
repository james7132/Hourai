#[macro_use]
extern crate lazy_static;

pub mod cache;
pub mod config;
pub mod init;
pub mod interactions;
pub mod models;
pub mod prelude;
pub mod util;

// Include the auto-generated protos as a module
pub mod proto {
    include!(concat!(env!("OUT_DIR"), "/proto/mod.rs"));
    use self::cache::{CachedRoleProto, CachedUserProto};
    use crate::models::user::{User, UserLike};
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

    impl From<User> for CachedUserProto {
        fn from(value: User) -> Self {
            let mut proto = Self::new();
            proto.set_id(value.id.get());
            proto.set_username(value.name().to_owned());
            proto.set_discriminator(value.discriminator as u32);
            proto.set_bot(value.bot());
            if let Some(avatar) = value.avatar_hash() {
                proto.set_avatar(avatar.to_owned());
            }
            proto
        }
    }
}

pub use twilight_gateway as gateway;
pub use twilight_http as http;
