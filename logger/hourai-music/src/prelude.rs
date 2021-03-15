pub use crate::player::PlayerExt;
use futures::channel::mpsc::UnboundedReceiver;
pub use hourai::prelude::*;
pub use std::net::SocketAddr;
pub use twilight_lavalink::{
    http::LoadedTracks, model::IncomingEvent, player::Player as TwilightPlayer, Lavalink, Node,
};

pub type LavalinkEventStream = UnboundedReceiver<IncomingEvent>;
