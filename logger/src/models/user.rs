use super::Snowflake;
use crate::proto::cache::CachedUserProto;
use twilight_model::id::UserId;
use twilight_model::user::User;
use twilight_model::guild::Member;

const DEFAULT_AVATAR_COUNT: u64 = 5;
const BASE_ASSET_URI: &str = "https://cdn.discordapp.com";

pub trait UserLike : Snowflake<UserId> {
    fn name(&self) -> &str;
    fn discriminator(&self) -> u16;
    fn avatar_hash(&self) -> Option<&str>;
    fn bot(&self) -> bool;

    fn avatar_url(&self) -> String {
        let format = if self.is_avatar_animated() { "gif" } else { "webp" };
        self.avatar_url_as(format, 1024)
    }

    fn avatar_url_as(&self, format: &str, size: u32) -> String {
        if let Some(hash) = self.avatar_hash() {
            format!("{}/avatars/{}/{}.{}?size={}", BASE_ASSET_URI, self.id(), hash, format, size)
        } else {
            self.default_avatar_url()
        }
    }

    fn default_avatar_url(&self) -> String {
        let idx = self.id().0 % DEFAULT_AVATAR_COUNT;
        format!("{}/embed/avatars/{}.png", BASE_ASSET_URI, idx)
    }

    fn is_avatar_animated(&self) -> bool {
        self.avatar_hash().map(|hash| hash.starts_with("a_")).unwrap_or(false)
    }

    fn display_name(&self) -> String {
        format!("{}#{:04}", self.name(), self.discriminator())
    }
}

impl Snowflake<UserId> for User {
    fn id(&self) -> UserId {
        self.id
    }
}

impl Snowflake<UserId> for Member {
    fn id(&self) -> UserId {
        self.user.id
    }
}

impl Snowflake<UserId> for CachedUserProto {
    fn id(&self) -> UserId {
        UserId(self.get_id())
    }
}

impl UserLike for User {
    fn name(&self) -> &str {
        self.name.as_str()
    }

    fn discriminator(&self) -> u16 {
        self.discriminator.parse::<u16>().unwrap()
    }

    fn avatar_hash(&self) -> Option<&str> {
        self.avatar.as_ref().map(|a| a.as_str())
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
        self.user.discriminator.parse::<u16>().unwrap()
    }

    fn avatar_hash(&self) -> Option<&str> {
        self.user.avatar.as_ref().map(|a| a.as_str())
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

    fn avatar_hash(&self) -> Option<&str> {
        if self.has_avatar() {
            Some(self.get_avatar())
        } else {
            None
        }
    }

    fn bot(&self) -> bool {
        self.get_bot()
    }

}
