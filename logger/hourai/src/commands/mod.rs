pub mod precondition;
pub mod prelude;

use std::sync::Arc;
use thiserror::Error;
use twilight_http::request::channel::message::*;
use twilight_model::channel::Message;

#[derive(Debug, Clone)]
pub struct Context<'a> {
    pub message: &'a Message,
    pub http: Arc<twilight_http::Client>,
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
    #[error("Too many arguments")]
    ExcessArguments,
    #[error("Missing argument")]
    MissingArgument,
}
