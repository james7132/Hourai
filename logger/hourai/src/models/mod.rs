pub mod message;
pub mod user;

pub use twilight_model::application;
pub use twilight_model::channel;
pub use twilight_model::gateway;
pub use twilight_model::guild;
pub use twilight_model::http;
pub use twilight_model::id;
pub use twilight_model::invite;
pub use twilight_model::oauth;
pub use twilight_model::util;
pub use twilight_model::voice;
pub use twilight_model::scheduled_event;

use chrono::prelude::DateTime;
use chrono::Utc;
use std::time::{Duration, UNIX_EPOCH};

pub use self::{message::MessageLike, user::UserLike};

pub trait Snowflake<I: SnowflakeId> {
    fn id(&self) -> I;

    fn created_at(&self) -> DateTime<Utc> {
        let timestamp = (self.id().as_u64() >> 22) + 1420070400000_u64;
        DateTime::<Utc>::from(UNIX_EPOCH + Duration::from_millis(timestamp))
    }
}

bitflags::bitflags! {
    pub struct RoleFlags : u64 {
        const DJ = 1;
        const MODERATOR = 1 << 2;
        const RESTORABLE = 1 << 3;
    }
}

pub trait SnowflakeId: Clone {
    fn as_u64(&self) -> u64;
}

impl<T> SnowflakeId for id::Id<T> {
    fn as_u64(&self) -> u64 {
        self.get()
    }
}
