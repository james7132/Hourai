use serde::Serialize;
use twilight_model::{
    id::{ChannelId, GuildId, UserId},
    voice::VoiceState,
};

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct CachedVoiceState {
    pub channel_id: Option<ChannelId>,
    pub guild_id: Option<GuildId>,
    pub user_id: UserId,
}

impl PartialEq<VoiceState> for CachedVoiceState {
    fn eq(&self, other: &VoiceState) -> bool {
        self.channel_id == other.channel_id
            && self.guild_id == other.guild_id
            && self.user_id == other.user_id
    }
}
