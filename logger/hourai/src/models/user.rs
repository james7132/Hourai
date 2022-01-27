use super::guild::Member;
use super::id::{marker::UserMarker, Id};
use super::Snowflake;
use crate::proto::cache::CachedUserProto;
pub use twilight_model::{user::*, util::image_hash::ImageHash};

const DEFAULT_AVATAR_COUNT: u64 = 5;
const BASE_ASSET_URI: &str = "https://cdn.discordapp.com";

pub trait UserLike: Snowflake<Id<UserMarker>> {
    fn name(&self) -> &str;
    fn discriminator(&self) -> u16;
    fn avatar_hash(&self) -> Option<ImageHash>;
    fn bot(&self) -> bool;

    fn avatar_url(&self) -> String {
        let is_animated = self
            .avatar_hash()
            .map(|hash| hash.is_animated())
            .unwrap_or(false);
        let format = if is_animated { "gif" } else { "webp" };
        self.avatar_url_as(format, 1024)
    }

    fn avatar_url_as(&self, format: &str, size: u32) -> String {
        if let Some(hash) = self.avatar_hash() {
            format!(
                "{}/avatars/{}/{}.{}?size={}",
                BASE_ASSET_URI,
                self.id(),
                hash,
                format,
                size
            )
        } else {
            self.default_avatar_url()
        }
    }

    fn default_avatar_url(&self) -> String {
        let idx = self.id().get() % DEFAULT_AVATAR_COUNT;
        format!("{}/embed/avatars/{}.png", BASE_ASSET_URI, idx)
    }

    fn display_name(&self) -> String {
        format!("{}#{:04}", self.name(), self.discriminator())
    }
}

impl Snowflake<Id<UserMarker>> for User {
    fn id(&self) -> Id<UserMarker> {
        self.id
    }
}

impl Snowflake<Id<UserMarker>> for Member {
    fn id(&self) -> Id<UserMarker> {
        self.user.id
    }
}

impl Snowflake<Id<UserMarker>> for CachedUserProto {
    fn id(&self) -> Id<UserMarker> {
        Id::new(self.get_id())
    }
}

impl UserLike for User {
    fn name(&self) -> &str {
        self.name.as_str()
    }

    fn discriminator(&self) -> u16 {
        self.discriminator
    }

    fn avatar_hash(&self) -> Option<ImageHash> {
        self.avatar
    }

    fn bot(&self) -> bool {
        self.bot
    }
}

impl UserLike for Member {
    fn name(&self) -> &str {
        self.user.name.as_str()
    }

    fn discriminator(&self) -> u16 {
        self.user.discriminator
    }

    fn avatar_hash(&self) -> Option<ImageHash> {
        self.user.avatar
    }

    fn bot(&self) -> bool {
        self.user.bot
    }
}

impl UserLike for CachedUserProto {
    fn name(&self) -> &str {
        self.get_username()
    }

    fn discriminator(&self) -> u16 {
        self.get_discriminator() as u16
    }

    fn avatar_hash(&self) -> Option<ImageHash> {
        if self.has_avatar() {
            ImageHash::parse(self.get_avatar().as_bytes()).ok()
        } else {
            None
        }
    }

    fn bot(&self) -> bool {
        self.get_bot()
    }
}
