use twilight_model::id::GuildId;
use twilight_lavalink::model::Play;
use std::convert::TryFrom;
use std::time::Duration;

#[derive(Clone)]
pub struct TrackInfo {
    pub title: Option<String>,
    pub author: Option<String>,
    pub uri: String,
    pub length: Duration,
    pub is_stream: bool,
}

pub struct Track {
    pub info: TrackInfo,
    pub encoded: Vec<u8>
}

impl Track {

    pub fn play(self, guild_id: GuildId) -> Play {
        Play::new(guild_id, self, None, None, false)
    }

}

impl Into<String> for Track {

    fn into(self) -> String {
        base64::encode(self.encoded)
    }

}

impl From<twilight_lavalink::http::TrackInfo> for TrackInfo {

    fn from(value: twilight_lavalink::http::TrackInfo) -> Self {
        Self {
            title: value.title,
            author: value.author,
            uri: value.uri,
            length: Duration::from_millis(value.length),
            is_stream: value.is_stream,
        }
    }

}

impl TryFrom<twilight_lavalink::http::Track> for Track {

    type Error = base64::DecodeError;

    fn try_from(value: twilight_lavalink::http::Track) -> Result<Self, Self::Error> {
        Ok(Self {
            info: TrackInfo::from(value.info),
            encoded: base64::decode(value.track)?
        })
    }

}

