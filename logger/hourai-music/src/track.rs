use hourai::models::{id::GuildId, user::User};
use twilight_lavalink::model::Play;
use std::convert::TryFrom;
use std::{fmt, time::Duration};
use tracing::error;

#[derive(Clone)]
pub struct TrackInfo {
    pub title: Option<String>,
    pub author: Option<String>,
    pub uri: String,
    pub length: Duration,
    pub is_stream: bool,
}

impl fmt::Display for TrackInfo {

    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(title) = &self.title {
            f.write_str(title.as_str())
        } else {
            f.write_str(self.uri.as_str())
        }
    }

}

#[derive(Clone)]
pub struct Track {
    pub requestor: User,
    pub info: TrackInfo,
    pub track: Vec<u8>
}

impl Track {
    pub fn play(&self, guild_id: GuildId) -> Play {
        Play::new(guild_id, base64::encode(&self.track), None, None, false)
    }
}

fn decode_track(track: String) -> std::result::Result<Vec<u8>, base64::DecodeError> {
    base64::decode(&track)
           .map_err(|err| {
               error!("Failed to decode track {}: {:?}", track, err);
               err
           })
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

impl TryFrom<(User, twilight_lavalink::http::Track)> for Track {
    type Error = base64::DecodeError;
    fn try_from(value: (User, twilight_lavalink::http::Track)) -> Result<Self, Self::Error> {
        Ok(Self {
            requestor: value.0,
            info: TrackInfo::from(value.1.info),
            track: decode_track(value.1.track)?
        })
    }
}
