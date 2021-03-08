mod user;
mod message;

use twilight_model::id::*;
use chrono::prelude::DateTime;
use chrono::Utc;
use std::time::{UNIX_EPOCH, Duration};

pub use self::{user::UserLike, message::MessageLike};

pub trait Snowflake<I: SnowflakeId> {
    fn id(&self) -> I;

    fn created_at(&self) -> DateTime::<Utc> {
        let timestamp = (self.id().as_u64() >> 22) + 1420070400000_u64;
        DateTime::<Utc>::from(UNIX_EPOCH + Duration::from_millis(timestamp))
    }
}

pub trait SnowflakeId: Clone + Copy {
    fn as_u64(&self) -> u64;
}

macro_rules! snowflake_id {
    ($id:ty) => {
        impl SnowflakeId for $id {
            fn as_u64(&self) -> u64 {
                self.0
            }
        }
    }
}

snowflake_id!(UserId);
snowflake_id!(MessageId);
snowflake_id!(ChannelId);
snowflake_id!(GuildId);
