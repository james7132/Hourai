pub mod prelude;
pub mod precondition;

use crate::cache::InMemoryCache;
use twilight_model::channel::Message;
use thiserror::Error;
use twilight_http::request::channel::message::*;

#[derive(Debug, Clone)]
pub struct Context<'a> {
    pub message: &'a Message,
    pub http: twilight_http::Client,
    pub cache: InMemoryCache,
}

impl Context<'_> {

    pub fn respond(&self) -> CreateMessage {
        self.http
            .create_message(self.message.channel_id)
            .reply(self.message.id)
    }

}

/// The sum type of all errors that might result from fetching
#[derive(Error, Debug)]
pub enum CommandError {
    #[error("User failed to satisfy preconditions: {}", .0)]
    FailedPrecondition(&'static str),
    #[error("Invalid Argument: {}", .0)]
    InvalidArgument(String),
    #[error("Something went wrong: {}", .0)]
    GenericFailure(&'static str),
}
