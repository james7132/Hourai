use crate::prelude::*;
use anyhow::{bail, Result};
use hourai::{
    models::{id::ChannelId, channel::Message, user::User},
    commands::{self, CommandError, precondition::*},
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
    -> Result<ChannelId> {
    let guild_id = require_in_guild(&ctx)?;

    let user = client.cache.voice_state(guild_id, ctx.message.author.id);
    let bot = client.get_channel(guild_id);
    if bot.is_some() && user != bot {
        bail!(CommandError::FailedPrecondition(
              "You must be in the same voice channel to play music."));
    } else if let Some(channel) = user {
        Ok(channel)
    } else {
        bail!(CommandError::FailedPrecondition(
              "You must be in a voice channel to play music."));
    }
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

async fn load_tracks(
    client: &Client<'static>,
    author: &User,
    node: &Node,
    query: &str) -> Vec<Track> {
    let response = match client.load_tracks(node, query).await {
        Ok(tracks) => tracks,
        Err(_) => return vec![],
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
            error!("Failed to load query `{}`", query);
            vec![]
        },
        _ => vec![],
    };

    tracks.into_iter().filter_map(|t| Track::try_from((author.clone(), t)).ok()).collect()
}

async fn play(client: &Client<'static>, ctx: commands::Context<'_>, query: Option<&str>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    let channel_id = require_in_voice_channel(client, &ctx)?;

    if query.is_none() {
        return pause(client, ctx, false).await;
    }

    let query = query.unwrap().trim_matches(
        |c: char| c.is_whitespace() || c == '<' || c == '>');
    let queries =vec![query.to_owned(),
                      format!("ytsearch:{}", query),
                      format!("scsearch:{}", query)];
    let node = client.get_node(guild_id).await?;
    let mut queue: Vec<Track> = Vec::new();
    for subquery in queries {
        queue = load_tracks(client, &ctx.message.author, &node, subquery.as_str()).await;
        if queue.len() > 0 {
            break;
        }
    }

    let duration = format_duration(queue.iter().map(|t| t.info.length).sum());
    let response = if queue.len() > 1 {
        format!(":notes: Added **{}** tracks ({}) to the music queue.",
                queue.len(), duration)
    } else if queue.len() == 1 {
        format!(":notes: Added `{}` ({}) to the music queue.",
                &queue[0].info, duration)
    } else {
        format!(":bulb: No results found for `{}`", query)
    };

    if queue.len() > 0 {
        if let Some(mut state) = client.states.get_mut(&guild_id) {
            state.value_mut().queue.extend(ctx.message.author.id, queue);
        } else {
            client.connect(guild_id, channel_id).await?;
            let mut state_queue = MusicQueue::new();
            state_queue.extend(ctx.message.author.id, queue);
            client.states.insert(guild_id, PlayerState {
                skip_votes: HashSet::new(),
                queue: state_queue
            });
            client.start_playing(guild_id).await?;
        }
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
    client.disconnect(guild_id).await?;
    ctx.respond()
       .content("The player has been stopped and the queue has been cleared")?
       .await?;
    Ok(())
}

async fn skip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_in_voice_channel(client, &ctx)?;
    require_playing!(client, ctx);
    let (votes, required) = client.mutate_state(guild_id, |state| {
        state.skip_votes.insert(ctx.message.author.id);
        let listeners = client.count_listeners(guild_id);
        (state.skip_votes.len(), listeners / 2)
    }).unwrap();

    let response = if  votes >= required {
        format!("Skipped `{}`", client.play_next(guild_id).await?.unwrap())
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
    let guild_id = require_in_guild(&ctx)?;
    require_in_voice_channel(client, &ctx)?;
    require_playing!(client, ctx);
    let response = client.mutate_state(guild_id, |state| {
        if let Some(count) = state.queue.clear_key(ctx.message.author.id) {
            format!("Removed **{}** tracks from the queue.", count)
        } else {
            "You currently do not have any tracks in the queue.".to_owned()
        }
    }).unwrap();
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn shuffle(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_in_voice_channel(client, &ctx)?;
    require_playing!(client, ctx);
    let response = client.mutate_state(guild_id, |state| {
        if let Some(count) = state.queue.shuffle(ctx.message.author.id) {
            format!("Shuffled **{}** tracks in the queue.", count)
        } else {
            "You currently do not have any tracks in the queue.".to_owned()
        }
    }).unwrap();
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn forceskip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    require_dj(client, &ctx).await?;
    require_playing!(client, ctx);
    let response = if let Some(previous) = client.play_next(guild_id).await? {
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
    secs -= minutes * 60;
    if hours > 0 {
        format!("{:02}:{:02}:{:02}", hours, minutes, secs)
    } else {
        format!("{:02}:{:02}", minutes, secs)
    }

}
