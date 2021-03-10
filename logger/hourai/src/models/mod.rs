pub mod user;
pub mod message;

pub use twilight_model::channel;
pub use twilight_model::gateway;
pub use twilight_model::guild;
pub use twilight_model::id;
pub use twilight_model::invite;
pub use twilight_model::oauth;
pub use twilight_model::voice;

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

snowflake_id!(id::UserId);
snowflake_id!(id::MessageId);
snowflake_id!(id::ChannelId);
snowflake_id!(id::GuildId);
