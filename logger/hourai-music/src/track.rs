use hourai::{
    models::{id::GuildId, user::User},
    proto::{cache::CachedUserProto, music_bot::TrackProto},
};
use std::convert::TryFrom;
use std::{fmt, time::Duration};
use tracing::error;
use twilight_lavalink::model::Play;

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
    pub requestor: CachedUserProto,
    pub info: TrackInfo,
    pub track: Vec<u8>,
}

impl Track {
    pub fn play(&self, guild_id: GuildId) -> Play {
        Play::new(guild_id, base64::encode(&self.track), None, None, false)
    }
}

fn decode_track(track: String) -> std::result::Result<Vec<u8>, base64::DecodeError> {
    base64::decode(&track).map_err(|err| {
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
            requestor: CachedUserProto::from(value.0),
            info: TrackInfo::from(value.1.info),
            track: decode_track(value.1.track)?,
        })
    }
}

impl From<Track> for TrackProto {
    fn from(value: Track) -> Self {
        let mut proto = Self::new();
        *proto.mut_requestor() = value.requestor;
        if let Some(title) = value.info.title {
            proto.set_title(title);
        }
        if let Some(author) = value.info.author {
            proto.set_author(author);
        }
        proto.set_uri(value.info.uri);
        proto.set_length(value.info.length.as_millis() as u64);
        proto.set_is_stream(value.info.is_stream);
        *proto.mut_track_data() = value.track;

        proto
    }
}

impl From<TrackProto> for Track {
    fn from(mut value: TrackProto) -> Self {
        let title = if value.has_title() {
            Some(value.take_title())
        } else {
            None
        };
        let author = if value.has_author() {
            Some(value.take_author())
        } else {
            None
        };

        Self {
            requestor: value.take_requestor(),
            info: TrackInfo {
                title,
                author,
                uri: value.take_uri(),
                length: Duration::from_millis(value.get_length()),
                is_stream: value.get_is_stream(),
            },
            track: value.take_track_data(),
        }
    }
}
