use crate::prelude::*;
use anyhow::{bail, Result};
use hourai::{
    models::{id::ChannelId, channel::Message},
    commands::{self, CommandError, precondition::*}
};
use twilight_command_parser::Command;
use twilight_lavalink::http::LoadType;
use crate::{Client, track::Track, player::PlayerState, queue::MusicQueue};
use std::{convert::TryFrom, collections::HashSet};

macro_rules! get_player {
    ($client:expr, $guild_id: expr) => {
        $client.lavalink.players().get($guild_id).unwrap().value()
    }
}

macro_rules! create_player {
    ($client:expr, $guild_id: expr) => {
        $client.lavalink.player($guild_id)
    }
}

pub async fn on_message_create(client: Client<'static>, evt: Message) -> Result<()> {
    debug!("Recieved message: {}", evt.content.as_str());
    if let Some(command) = client.parser.parse(evt.content.as_str()) {
        debug!("Potential command: {}", evt.content.as_str());
        let ctx = commands::Context {
            message: &evt,
            http: client.http_client.clone(),
            cache: client.cache.clone(),
        };

        let result = match command {
            Command { name: "play", arguments, .. } =>
                play(&client, ctx, arguments.into_remainder()).await,
            Command { name: "pause", .. } => pause(&client, ctx, true).await,
            Command { name: "stop", .. } => stop(&client, ctx).await,
            Command { name: "shuffle",  .. } => shuffle(&client, ctx).await,
            Command { name: "skip", .. } => skip(&client, ctx).await,
            Command { name: "forceskip", .. } => forceskip(&client, ctx).await,
            Command { name: "remove", arguments, .. } => Ok(()),
            Command { name: "removeall", .. } => remove_all(&client, ctx).await,
            Command { name: "nowplaying", .. } => Ok(()),
            Command { name: "np", .. } => Ok(()),
            Command { name: "queue", .. } => Ok(()),
            Command { name: "volume", arguments, .. } =>
                // TODO(james7132): Do proper argument parsing.
                volume(&client, ctx, Some(100)).await,
            _ => {
                debug!("Failed to find command: {}", evt.content.as_str());
                Ok(())
            }
        };

        if let Err(err) = result {
            match err.downcast::<CommandError>() {
                Ok(command_error) => {
                    client.http_client
                        .create_message(evt.channel_id)
                        .reply(evt.id)
                        .content(format!(":x: {}", command_error))?
                        .await?;
                },
                Err(err) => bail!(err),
            }
        }
    }
    Ok(())
}


fn require_in_voice_channel(client: &Client<'static>, ctx: &commands::Context<'_>)
    -> Result<Option<ChannelId>> {
    let guild_id = require_in_guild(&ctx)?;

    let user = client.cache.voice_state(guild_id, ctx.message.author.id);
    let bot = client.cache.voice_state(guild_id, client.user_id);
    if user.is_none() {
        bail!(CommandError::FailedPrecondition(
              "You must be in a voice channel to play music."));
    } else if bot.is_some() && user != bot {
        bail!(CommandError::FailedPrecondition(
              "You must be in the same voice channel to play music."));
    }

    Ok(user)
}

macro_rules! require_playing {
    ($client:expr, $ctx:expr) => {
        $client.states
               .get_mut(&require_in_guild(&$ctx)?)
               .ok_or_else(|| CommandError::FailedPrecondition("No music is currently playing."))
               .and_then(|state| {
                   if state.value().currently_playing().is_some() {
                       Ok(state)
                   } else {
                       Err(CommandError::FailedPrecondition("No music is currently playing."))
                   }
               })?
    }
}

async fn require_dj(client: &Client<'static>, ctx: &commands::Context<'_>) -> Result<()> {
    require_in_voice_channel(client, &ctx)?;
    Ok(())
}

async fn play(client: &Client<'static>, ctx: commands::Context<'_>, query: Option<&str>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    let user_channel_id = require_in_voice_channel(client, &ctx)?;

    if query.is_none() {
        return pause(client, ctx, false).await;
    }

    let node = client.get_node(guild_id).await?;
    let response = match client.load_tracks(node, query.unwrap()).await {
        Ok(tracks) => tracks,
        Err(_) => bail!(CommandError::GenericFailure("Failed to load track(s).")),
    };

    let tracks = match response {
        LoadedTracks { load_type: LoadType::TrackLoaded, tracks, .. } => {
            assert!(tracks.len() > 0);
            vec![tracks[0].clone()]
        },
        LoadedTracks { load_type: LoadType::SearchResult, tracks, .. } => {
            // TODO(james7132): This could be improved by doing a edit distance from
            // the query for similarity matching.
            assert!(tracks.len() > 0);
            vec![tracks[0].clone()]
        },
        LoadedTracks { load_type: LoadType::PlaylistLoaded, tracks, playlist_info, .. } => {
            if let Some(idx) = playlist_info.selected_track {
                vec![tracks[idx as usize].clone()]
            } else {
                tracks
            }
        },
        LoadedTracks { load_type: LoadType::LoadFailed, .. } => {
            bail!(CommandError::GenericFailure("Failed to load tracks."));
        },
        _ => vec![],
    };

    let queue: Vec<Track> = tracks.into_iter()
                                  .filter_map(|t| Track::try_from(t).ok())
                                  .collect();
    let duration = format_duration(queue.iter().map(|t| t.info.length).sum());

    let response = if queue.len() > 1 {
        format!(":notes: Added **{}** tracks ({}) to the music queue.",
                queue.len(), duration)
    } else if queue.len() == 1 {
        format!(":notes: Added `{}` ({}) to the music queue.",
                &queue[0].info, duration)
    } else {
        format!(":bulb: No results found for `{}`", query.unwrap())
    };

    if let Some(mut state) = client.states.get_mut(&guild_id) {
        state.value_mut().queue.extend(ctx.message.author.id, queue);
    } else {
        let channel_id = user_channel_id.unwrap();
        create_player!(client, guild_id).await?.value()
            .connect(client.gateway.clone(), channel_id);
        let mut state_queue = MusicQueue::new();
        state_queue.extend(ctx.message.author.id, queue);
        client.states.insert(guild_id, PlayerState {
            channel_id: channel_id,
            skip_votes: HashSet::new(),
            queue: state_queue
        });
        client.play_next(guild_id)?;
    }

    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn pause(client: &Client<'static>, ctx: commands::Context<'_>, pause: bool) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_dj(client, &ctx).await?;
    require_playing!(client, ctx);
    get_player!(client, &guild_id).set_pause(pause)?;
    let response = if pause {
        "The music bot has been paused."
    } else {
        "The music bot has been unpaused."
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn stop(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_dj(client, &ctx).await?;
    require_playing!(client, ctx);
    client.disconnect(guild_id);
    ctx.respond()
       .content("The player has been stopped and the queue has been cleared")?
       .await?;
    Ok(())
}

async fn skip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_in_voice_channel(client, &ctx)?;
    let (votes, required) = {
        let mut state = require_playing!(client, ctx);
        state.value_mut().skip_votes.insert(ctx.message.author.id);
        let listeners = client.cache.voice_channel_users(state.channel_id).len();
        // TODO(james7132): Make this ratio configurable.
        (state.skip_votes.len(), listeners / 2)
    };

    let response = if  votes >= required {
        format!("Skipped `{}`", client.play_next(guild_id)?.unwrap())
    } else {
        format!("Total votes: `{}/{}`.", votes, required)
    };

    ctx.respond().content(response)?.await?;
    Ok(())
}

// TODO(james7132): Properly implmement
async fn remove(client: &Client<'static>, ctx: commands::Context<'_>, index: usize) -> Result<()> {
    require_in_voice_channel(client, &ctx)?;
    require_playing!(client, ctx);
    ctx.respond().content("Skipped.")?.await?;
    Ok(())
}

async fn remove_all(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    require_in_voice_channel(client, &ctx)?;
    let mut state = require_playing!(client, ctx);
    let response = if let Some(count) = state.value_mut().queue.clear_key(ctx.message.author.id) {
        format!("Removed **{}** tracks from the queue.", count)
    } else {
        "You currently do not have any tracks in the queue.".to_owned()
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn shuffle(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    require_in_voice_channel(client, &ctx)?;
    let mut state = require_playing!(client, ctx);
    let response = if let Some(count) = state.value_mut().queue.shuffle(ctx.message.author.id) {
        format!("Shuffled **{}** tracks in the queue.", count)
    } else {
        "You currently do not have any tracks in the queue.".to_owned()
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn forceskip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_dj(client, &ctx).await?;
    require_playing!(client, ctx);
    let response = if let Some(previous) = client.play_next(guild_id)? {
        format!("Skipped `{}`.", previous)
    } else {
        "There is nothing in the queue right now.".to_owned()
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn volume(client: &Client<'static>, ctx: commands::Context<'_>, volume: Option<i64>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_playing!(client, ctx);
    let response = if let Some(vol) = volume {
        require_dj(client, &ctx).await?;
        if vol < 0 || vol > 150 {
            bail!(CommandError::InvalidArgument(
                    "Volume must be between 0 and 150.".into()));
        }
        get_player!(client, &guild_id).set_volume(vol as u8)?;
        format!("Set volume to `{}`.", vol)
    } else {
        format!("Current volume is `{}`.", get_player!(client, &guild_id).volume_ref())
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn queue(client: &Client<'static>, _: commands::Context<'_>) -> Result<()> {
    // TODO(james7132): Implement.
    Ok(())
}

async fn now_playing(client: &Client<'static>, _: commands::Context<'_>) -> Result<()> {
    // TODO(james7132): Implement.
    Ok(())
}

fn format_duration(duration: Duration) -> String {
    let mut secs = duration.as_secs();
    let hours = secs / 3600;
    secs -= hours * 3600;
    let minutes = secs / 60;
    secs -= secs * 60;
    if hours > 0 {
        format!("{:02}:{:02}:{:02}", hours, minutes, secs)
    } else {
        format!("{:02}:{:02}", minutes, secs)
    }
}
