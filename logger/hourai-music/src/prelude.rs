pub use crate::player::PlayerExt;
pub use hourai::prelude::*;
pub use std::net::SocketAddr;
pub use twilight_lavalink::{
    http::LoadedTracks, model::IncomingEvent, node::IncomingEvents,
    player::Player as TwilightPlayer, Lavalink, Node,
};

pub fn format_duration(duration: Duration) -> String {
    let mut secs = duration.as_secs();
    let hours = secs / 3600;
    secs -= hours * 3600;
    let minutes = secs / 60;
    secs -= minutes * 60;
    if hours > 0 {
        format!("{:02}:{:02}:{:02}", hours, minutes, secs)
    } else {
        format!("{:02}:{:02}", minutes, secs)
    }
}
