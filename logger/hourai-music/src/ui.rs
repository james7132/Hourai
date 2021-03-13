use anyhow::{bail, Result};
use hourai::models::{id::*, channel::embed::Embed, UserLike};
use twilight_embed_builder::*;
use std::time::{Duration, Instant};
use tokio::sync::oneshot::error::TryRecvError;

const PROGRESS_BAR_WIDTH: usize = 12;

pub struct MessageUI {
    cancel: tokio::sync::oneshot::Sender<()>
}

impl MessageUI {
    pub fn run<U>(ui: U, update_interval: Duration) -> Self
        where U: Updateable + Send + 'static
    {
        let (tx, mut rx) = tokio::sync::oneshot::channel();
        tokio::spawn(async move {
            while let Ok(_) = ui.update().await {
                match rx.try_recv() {
                    Err(TryRecvError::Empty) =>
                        tokio::time::sleep(update_interval).await,
                    _ => break,
                }
            }
        });
        Self { cancel: tx }
    }

    pub fn cancel(self) {
        let _ = self.cancel.send(());
    }
}

#[async_trait::async_trait]
pub trait Updateable: Sync {
    async fn update(&self) -> Result<()>;
}

pub trait EmbedUIBuilder : Sized + Default + Sync + Send {
    fn build_content(&self, ui: &EmbedUI<Self>) -> String;
    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed>;
}

pub struct EmbedUI<T> where T : EmbedUIBuilder {
    pub client: crate::Client<'static>,
    pub guild_id: GuildId,
    pub channel_id: ChannelId,
    pub message_id: MessageId,
    pub expiration: Instant,
    builder: T,
}

#[async_trait::async_trait]
impl<T: EmbedUIBuilder> Updateable for EmbedUI<T> {
    async fn update(&self) -> Result<()> {
        self.client
            .http_client
            .update_message(self.channel_id, self.message_id)
            .content(self.builder.build_content(self))?
            .embed(self.builder.build_embed(self)?)?
            .await?;
        if self.expiration < Instant::now() {
            Ok(())
        } else {
            bail!("Expired.")
        }
    }
}

#[derive(Default)]
pub struct NowPlayingUI;
#[derive(Default)]
pub struct QueueUI;

impl EmbedUIBuilder for NowPlayingUI {
    fn build_content(&self, _: &EmbedUI<Self>) -> String {
        ":notes: **Now Playing**".to_owned()
    }

    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed> {
        let track ={
            let cp = ui.client.states.get(&ui.guild_id)
                       .and_then(|kv| kv.currently_playing().map(|kv| kv.1));
            match cp {
                Some(track) => track,
                None => return not_playing_embed(),
            }
        };
        let (complete, paused) = {
            match ui.client.lavalink.players().get(&ui.guild_id) {
                Some(kv) => {
                    let player = kv.value();
                    let pos = player.position() as f64;
                    let time = player.time_ref() as f64;
                    if time == 0.0 {
                        (f64::INFINITY, player.paused())
                    } else {
                        (pos / time, player.paused())
                    }
                },
                None => (f64::INFINITY, true)
            }
        };

        let prefix = if paused { "⏸️" } else { "▶️"};
        let suffix= if paused { "🔇" } else { "🔊"};
        let progress = format!("{}{}{}", prefix, progress_bar(complete), suffix);

        Ok(EmbedBuilder::new()
            .author(EmbedAuthorBuilder::new()
                .name(track.requestor.display_name())?
                .icon_url(ImageSource::url(track.requestor.avatar_url())?))
            .title(track.info.title.unwrap_or_else(|| "Unknown".to_owned()))?
            .description(progress)?
            .url(track.info.uri.clone())
            .build()?)
    }
}

impl EmbedUIBuilder for QueueUI {
    fn build_content(&self, _: &EmbedUI<Self>) -> String {
        String::new()
    }

    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed> {
        let track =
            ui.client.states.get(&ui.guild_id)
              .and_then(|kv| kv.currently_playing().map(|kv| kv.1));
        if track.is_none() {
            return not_playing_embed();
        }
        return not_playing_embed();
    }
}

fn not_playing_embed() -> Result<Embed> {
    let progress = format!("⏹️{}🔇", progress_bar(f64::INFINITY));
    Ok(EmbedBuilder::new()
        .title("**No music playing**")?
        .description(progress)?
        .build()?)
}

fn progress_bar(ratio: f64) -> String {
    (0..PROGRESS_BAR_WIDTH)
        .into_iter()
        .map(|idx| {
            if idx == (ratio * (PROGRESS_BAR_WIDTH as f64)) as usize {
                '🔘'
            } else {
                '▬'
            }
        })
        .collect()
}
