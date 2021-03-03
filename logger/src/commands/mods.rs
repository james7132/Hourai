use futures::future::Future;
use twilight_model::message::Message;

pub type Command = Fn(Context) -> impl Future<Output=Result<(), CommandError>>;

#[derive(Debug, Clone)]
struct Context {
    pub message: Message,
    pub http: twilight_http::Client,
}

pub enum CommandError {
}
