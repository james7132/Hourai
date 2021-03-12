pub use hourai::prelude::*;
pub use twilight_lavalink::{
    Lavalink, Node,
    player::Player as TwilightPlayer,
    http::LoadedTracks,
    model::IncomingEvent
};
pub use crate::player::PlayerExt;
pub use std::net::SocketAddr;
use futures::channel::mpsc::UnboundedReceiver;

pub type LavalinkEventStream = UnboundedReceiver<IncomingEvent>;
