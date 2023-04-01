use crate::{buttons, prelude::*, track::Track, Client};
use anyhow::Result;
use hourai::{
    interactions::{CommandContext, InteractionContext, Response},
    models::{
        channel::message::{embed::Embed, component::*},
        id::{marker::*, Id},
        UserLike,
    },
    proto::message_components::MusicUIType,
};
use std::time::{Duration, Instant};
use tokio::sync::oneshot::error::TryRecvError;
use twilight_util::builder::embed::*;

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
        let expiration = Instant::now() + Duration::from_secs(600);
        tokio::spawn(async move {
            tokio::time::sleep(update_interval).await;
            while Instant::now() < expiration {
                match ui.update().await {
                    Ok(_) => match rx.try_recv() {
                        Err(TryRecvError::Empty) => tokio::time::sleep(update_interval).await,
                        _ => break,
                    },
                    Err(err) => {
                        tracing::error!("Terminating message UI: {} ({:?})", err, err);
                        break;
                    }
                }
            }
            tracing::info!("Terminating message UI: timeout");
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
    fn build_components(&self, _: &EmbedUI<Self>) -> Result<Vec<Component>> {
        Ok(vec![])
    }
}

pub struct EmbedUI<T>
where
    T: EmbedUIBuilder + Default,
{
    pub client: crate::Client,
    pub context: CommandContext,
    pub guild_id: Id<GuildMarker>,
    builder: T,
}

impl<T> EmbedUI<T>
where
    T: EmbedUIBuilder + Default,
{
    pub async fn create(client: Client, ctx: CommandContext) -> Result<Self> {
        let guild_id = ctx.guild_id()?;

        let dummy = Self {
            client,
            context: ctx.clone(),
            guild_id,
            builder: T::default(),
        };

        ctx.reply(
            Response::direct()
                .content(&dummy.builder.build_content(&dummy))
                .embed(dummy.builder.build_embed(&dummy)?)
                .components(&dummy.builder.build_components(&dummy)?),
        )
        .await?;

        Ok(dummy)
    }
}

#[async_trait::async_trait]
impl<T: EmbedUIBuilder> Updateable for EmbedUI<T> {
    async fn update(&self) -> Result<()> {
        self.context
            .http()
            .interaction(self.context.application_id())
            .update_response(&self.context.command.token)
            .content(Some(&self.builder.build_content(self)))?
            .embeds(Some(&[self.builder.build_embed(self)?]))?
            .components(Some(&self.builder.build_components(self)?))?
            .await?;
        Ok(())
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
        build_np_embed(ui)
    }

    fn build_components(&self, _: &EmbedUI<Self>) -> Result<Vec<Component>> {
        Ok(vec![Component::ActionRow(ActionRow {
            components: vec![
                buttons::volume_down_button(MusicUIType::MUSIC_UI_TYPE_NOW_PLAYING),
                buttons::volume_up_button(MusicUIType::MUSIC_UI_TYPE_NOW_PLAYING),
                buttons::play_button(MusicUIType::MUSIC_UI_TYPE_NOW_PLAYING),
                buttons::stop_button(MusicUIType::MUSIC_UI_TYPE_NOW_PLAYING),
                buttons::skip_button(MusicUIType::MUSIC_UI_TYPE_NOW_PLAYING),
            ],
        })])
    }
}

fn build_np_embed<T: EmbedUIBuilder>(ui: &EmbedUI<T>) -> Result<Embed> {
    let track = match ui.client.currently_playing(ui.guild_id) {
        Some(track) => track,
        None => return not_playing_embed(),
    };
    let volume = match ui.client.lavalink.players().get(&ui.guild_id) {
        Some(player) => player.volume(),
        None => return not_playing_embed(),
    };

    Ok(EmbedBuilder::new()
        .author(
            EmbedAuthorBuilder::new(track.requestor.display_name())
                .icon_url(ImageSource::url(track.requestor.avatar_url())?),
        )
        .title(track.info.title.unwrap_or_else(|| "Unknown".to_owned()))
        .description(build_progress_bar(&ui))
        .url(track.info.uri.clone())
        .footer(EmbedFooterBuilder::new(format!("Volume: {}", volume)))
        .build())
}

impl EmbedUIBuilder for QueueUI {
    fn build_content(&self, ui: &EmbedUI<Self>) -> String {
        let track = match ui.client.currently_playing(ui.guild_id) {
            Some(track) => track,
            None => return ":notes: **Now Playing...**".to_owned(),
        };
        let queue_len = ui.client.get_queue(ui.guild_id, |q| q.len()).unwrap();
        let total_duration = ui
            .client
            .get_queue(ui.guild_id, |q| {
                q.iter().map(|kv| kv.value.info.length).sum()
            })
            .unwrap();
        format!(
            ":arrow_forward: **{}**\n:notes: Current Queue | {} entries | {}",
            track.info,
            queue_len,
            format_duration(total_duration)
        )
    }

    fn build_embed(&self, ui: &EmbedUI<Self>) -> Result<Embed> {
        fn format_track(idx: usize, track: &Track) -> String {
            format!(
                "`{}.` `[{}]` **[{}]({})** - <@{}>",
                idx,
                format_duration(track.info.length),
                track.info,
                track.info.uri,
                track.requestor.get_id()
            )
        }

        let pages = match ui.client.get_queue(ui.guild_id, |q| q.len()) {
            Some(len) => len / TRACKS_PER_PAGE + 1,
            None => return not_playing_embed(),
        };

        let current_page = match ui.client.queue_page(ui.guild_id) {
            Some(current_page) => current_page % pages as i64,
            None => return not_playing_embed(),
        };

        let description = ui
            .client
            .get_queue(ui.guild_id, |q| {
                q.iter()
                    .enumerate()
                    // Add one for the currently playing song.
                    .skip(current_page as usize * TRACKS_PER_PAGE + 1)
                    .take(TRACKS_PER_PAGE)
                    .map(|kv| format_track(kv.0, &kv.1.value))
                    .collect::<Vec<String>>()
                    .join("\n")
            })
            .unwrap();
        if description.is_empty() {
            build_np_embed(&ui)
        } else {
            let footer = format!("Page {}/{}", current_page + 1, pages + 1);
            Ok(EmbedBuilder::new()
                .description(description)
                .footer(EmbedFooterBuilder::new(footer))
                .build())
        }
    }

    fn build_components(&self, _: &EmbedUI<Self>) -> Result<Vec<Component>> {
        Ok(vec![Component::ActionRow(ActionRow {
            components: vec![
                buttons::previous_button(MusicUIType::MUSIC_UI_TYPE_QUEUE),
                buttons::volume_down_button(MusicUIType::MUSIC_UI_TYPE_QUEUE),
                buttons::play_button(MusicUIType::MUSIC_UI_TYPE_QUEUE),
                buttons::volume_up_button(MusicUIType::MUSIC_UI_TYPE_QUEUE),
                buttons::next_button(MusicUIType::MUSIC_UI_TYPE_QUEUE),
            ],
        })])
    }
}

fn build_progress_bar<T: EmbedUIBuilder + Default>(ui: &EmbedUI<T>) -> String {
    let (pos, paused) = match ui.client.lavalink.players().get(&ui.guild_id) {
        Some(player) => (player.position(), player.paused()),
        None => (0, true),
    };

    let length = match ui.client.currently_playing(ui.guild_id) {
        Some(track) => track.info.length,
        None => Duration::from_millis(i64::MAX as u64),
    };
    let pos = Duration::from_millis(pos as u64);
    let complete = (pos.as_millis() as f64) / (length.as_millis() as f64);
    let prefix = if paused {
        ":pause_button:"
    } else {
        ":arrow_forward:"
    };
    let suffix = if paused { ":mute:" } else { ":loud_sound:" };
    let time_display = if pos.as_millis() as i64 == i64::MAX {
        "LIVE".to_owned()
    } else {
        format!("{}/{}", format_duration(pos), format_duration(length))
    };
    format!(
        "{}{}`[{}]`{}",
        prefix,
        progress_bar(complete),
        time_display,
        suffix
    )
}

fn not_playing_embed() -> Result<Embed> {
    let progress = format!(":stop_button:{}:mute:", progress_bar(f64::INFINITY));
    Ok(EmbedBuilder::new()
        .title("**No music playing**")
        .description(progress)
        .build())
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
