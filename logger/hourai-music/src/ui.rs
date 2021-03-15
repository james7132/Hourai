use anyhow::{bail, Result};
use hourai::{
    commands,
    models::{
        UserLike,
        channel::embed::Embed,
        id::*,
    },
};
use std::time::{Duration, Instant};
use crate::{prelude::*, track::Track, Client};
use tokio::sync::oneshot::error::TryRecvError;
use twilight_embed_builder::*;

const PROGRESS_BAR_WIDTH: usize = 12;
const TRACKS_PER_PAGE: usize = 10;

pub struct MessageUI {
    cancel: tokio::sync::oneshot::Sender<()>,
}

impl MessageUI {
    pub fn run<U>(ui: U, update_interval: Duration) -> Self
    where
        U: Updateable + Send + 'static,
    {
        let (tx, mut rx) = tokio::sync::oneshot::channel();
        tokio::spawn(async move {
            while let Ok(_) = ui.update().await {
                match rx.try_recv() {
                    Err(TryRecvError::Empty) => tokio::time::sleep(update_interval).await,
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

pub trait EmbedUIBuilder: Sized + Default + Sync + Send {
    fn build_content(&self, ui: &EmbedUI<Self>) -> String;
    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed>;
}

pub struct EmbedUI<T>
where
    T: EmbedUIBuilder + Default,
{
    pub client: crate::Client<'static>,
    pub guild_id: GuildId,
    pub channel_id: ChannelId,
    pub message_id: MessageId,
    pub expiration: Instant,
    builder: T,
}

impl<T> EmbedUI<T>
where
    T: EmbedUIBuilder + Default,
{
    pub async fn create(client: Client<'static>, ctx: commands::Context<'_>) -> Result<Self> {
        let guild_id = commands::precondition::require_in_guild(&ctx)?;
        let channel_id = ctx.message.channel_id;

        let mut dummy = Self {
            client, guild_id, channel_id,
            message_id: MessageId(0),
            expiration: Instant::now() + Duration::from_secs(300),
            builder: T::default(),
        };

        let message = dummy.client.http_client
            .create_message(channel_id)
            .content(dummy.builder.build_content(&dummy))?
            .embed(dummy.builder.build_embed(&dummy)?)?
            .await?;

        dummy.message_id = message.id;
        Ok(dummy)
    }
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
pub struct QueueUI {
    page: usize
}

impl EmbedUIBuilder for NowPlayingUI {
    fn build_content(&self, _: &EmbedUI<Self>) -> String {
        ":notes: **Now Playing**".to_owned()
    }

    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed> {
        let track = match ui.client.currently_playing(ui.guild_id) {
            Some(track) => track,
            None => return not_playing_embed(),
        };

        Ok(EmbedBuilder::new()
            .author(
                EmbedAuthorBuilder::new()
                    .name(track.requestor.display_name())?
                    .icon_url(ImageSource::url(track.requestor.avatar_url())?),
            )
            .title(track.info.title.unwrap_or_else(|| "Unknown".to_owned()))?
            .description(build_progress_bar(&ui))?
            .url(track.info.uri.clone())
            .build()?)
    }
}

impl EmbedUIBuilder for QueueUI {
    fn build_content(&self, ui: &EmbedUI<Self>) -> String {
        let track = match ui.client.currently_playing(ui.guild_id) {
            Some(track) => track,
            None => return ":notes: **Now Playing...**".to_owned(),
        };
        let queue_len = ui.client.get_queue(ui.guild_id, |q| q.len()).unwrap();
        let total_duration = ui.client.get_queue(ui.guild_id, |q| {
            q.iter().map(|kv| kv.value.info.length).sum()
        }).unwrap();
        format!(":arrow_forward: **{}**\n:notes: Current Queue | {} entries | {}",
                track.info, queue_len, format_duration(total_duration))
    }

    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed> {
        fn format_track(idx: usize, track: &Track) -> String {
            format!("`{}.` `[{}]` **{} - <@{}>**",
                    idx, format_duration(track.info.length), track.info, track.requestor.id)
        }

        let pages = match ui.client.get_queue(ui.guild_id, |q| q.len()) {
            Some(len) => len / TRACKS_PER_PAGE,
            None => return not_playing_embed(),
        };
        let description = ui.client.get_queue(ui.guild_id, |q| {
            q.iter()
             .enumerate()
             // Add one for the currently playing song.
             .skip(self.page * TRACKS_PER_PAGE + 1)
             .take(TRACKS_PER_PAGE)
             .map(|kv| format_track(kv.0, &kv.1.value))
             .collect::<Vec<String>>()
             .join("\n")
        }).unwrap();
        let footer = format!("Page {}/{}", self.page + 1, pages + 1);
        Ok(EmbedBuilder::new()
            .description(description)?
            .footer(EmbedFooterBuilder::new(footer)?)
            .build()?)
    }
}

fn build_progress_bar<T: EmbedUIBuilder + Default>(ui: &EmbedUI<T>) -> String {
    let (complete, paused) = match ui.client.lavalink.players().get(&ui.guild_id) {
        Some(kv) => {
            let player = kv.value();
            let pos = player.position() as f64;
            let time = player.time_ref() as f64;
            if time == 0.0 {
                (f64::INFINITY, player.paused())
            } else {
                (pos / time, player.paused())
            }
        }
        None => (f64::INFINITY, true),
    };

    let prefix = if paused { ":pause_button:" } else { ":arrow_forward:" };
    let suffix = if paused { ":mute:" } else { ":loud_sound:" };
    format!("{}{}{}", prefix, progress_bar(complete), suffix)
}

fn not_playing_embed() -> Result<Embed> {
    let progress = format!(":stop_button:{}:mute:", progress_bar(f64::INFINITY));
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
                ":radio_button:"
            } else {
                "▬"
            }
        })
        .collect()
}
