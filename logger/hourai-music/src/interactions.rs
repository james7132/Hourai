use crate::prelude::*;
use crate::{interaction_ui::*, player::PlayerState, queue::MusicQueue, track::Track, Client};
use anyhow::{bail, Result};
use hourai::{
    interactions::{Command, CommandContext, CommandError, Response},
    models::{
        id::{ChannelId, GuildId, RoleId},
        UserLike,
    },
    proto::guild_configs::MusicConfig,
};
use std::{collections::HashSet, convert::TryFrom};
use twilight_lavalink::http::LoadType;

macro_rules! get_player {
    ($client:expr, $guild_id: expr) => {
        $client.lavalink.players().get($guild_id).unwrap()
    };
}

pub async fn handle_command(client: Client<'static>, ctx: CommandContext) -> Result<()> {
    let result = match ctx.command() {
        Command::SubCommand("music", "play") => play(&client, &ctx).await,
        Command::SubCommand("music", "pause") => pause(&client, &ctx, true).await,
        Command::SubCommand("music", "stop") => stop(&client, &ctx).await,
        Command::SubCommand("music", "shuffle") => shuffle(&client, &ctx).await,
        Command::SubCommand("music", "skip") => skip(&client, &ctx).await,
        Command::SubCommand("music", "remove") => remove(&client, &ctx).await,
        Command::SubCommand("music", "nowplaying") => {
            now_playing(&client, ctx).await?;
            return Ok(());
        }
        Command::SubCommand("music", "queue") => {
            queue(&client, ctx).await?;
            return Ok(());
        }
        Command::SubCommand("music", "volume") => volume(&client, &ctx).await,
        _ => return Err(anyhow::Error::new(CommandError::UnknownCommand)),
    };

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(err) => {
            let response = Response::ephemeral();
            if let Some(command_err) = err.downcast_ref::<CommandError>() {
                ctx.reply(response.content(format!(":x: Error: {}", command_err)))
                    .await?;
                Ok(())
            } else {
                // TODO(james7132): Add some form of tracing for this.
                ctx.reply(response.content(":x: Fatal Error: Internal Error has occured."))
                    .await?;
                Err(err)
            }
        }
    }
}

async fn require_in_voice_channel(
    client: &Client<'static>,
    ctx: &CommandContext,
) -> Result<(GuildId, ChannelId)> {
    let guild_id = ctx.guild_id()?;

    let mut redis = client.redis.clone();
    let user: Option<u64> = hourai_redis::CachedVoiceState::get_channel(guild_id, ctx.user().id)
        .query_async(&mut redis)
        .await?;
    let user = user.and_then(ChannelId::new);
    let bot = client.get_channel(guild_id);
    if bot.is_some() && user != bot {
        bail!(CommandError::FailedPrecondition(
            "You must be in the same voice channel to play music."
        ));
    } else if let Some(channel) = user {
        Ok((guild_id, channel))
    } else {
        bail!(CommandError::FailedPrecondition(
            "You must be in a voice channel to play music."
        ));
    }
}

fn require_playing(client: &Client<'static>, ctx: &CommandContext) -> Result<GuildId> {
    let guild_id = ctx.guild_id()?;
    client
        .states
        .get(&guild_id)
        .filter(|state| state.value().is_playing())
        .ok_or(CommandError::FailedPrecondition(
            "No music is currently playing.",
        ))?;
    Ok(guild_id)
}

fn is_dj(config: &MusicConfig, roles: &[RoleId]) -> bool {
    let dj_roles = config.get_dj_role_id();
    roles.iter().any(|id| dj_roles.contains(&id.get()))
}

/// Requires that the author is a DJ on the server to use the command.
async fn require_dj(client: &Client<'static>, ctx: &CommandContext) -> Result<()> {
    let (guild_id, _) = require_in_voice_channel(client, &ctx).await?;
    if let Some(ref member) = ctx.command.member {
        let config = client.get_config(guild_id).await?;
        if is_dj(&config, &member.roles) {
            return Ok(());
        }
    }
    bail!(CommandError::FailedPrecondition(
        "User must be a DJ to use this command."
    ))
}

async fn load_tracks(
    client: &Client<'static>,
    node: &Node,
    query: &str,
) -> Option<Vec<twilight_lavalink::http::Track>> {
    let response = match client.load_tracks(node, query).await {
        Ok(tracks) => tracks,
        Err(_) => return None,
    };

    if response.tracks.is_empty() {
        return None;
    }

    let tracks = match response {
        LoadedTracks {
            load_type: LoadType::TrackLoaded,
            tracks,
            ..
        } => {
            vec![tracks[0].clone()]
        }
        LoadedTracks {
            load_type: LoadType::SearchResult,
            tracks,
            ..
        } => {
            // TODO(james7132): This could be improved by doing a edit distance from
            // the query for similarity matching.
            vec![tracks[0].clone()]
        }
        LoadedTracks {
            load_type: LoadType::PlaylistLoaded,
            tracks,
            playlist_info,
            ..
        } => playlist_info
            .selected_track
            .and_then(|idx| tracks.get(idx as usize))
            .map(|track| vec![track.clone()])
            .unwrap_or_else(|| tracks),
        LoadedTracks {
            load_type: LoadType::LoadFailed,
            ..
        } => {
            error!("Failed to load query `{}`", query);
            return None;
        }
        _ => return None,
    };

    Some(tracks)
}

async fn play(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let (guild_id, channel_id) = require_in_voice_channel(client, &ctx).await?;
    let user = ctx.user();
    let query = match ctx.get_string("query") {
        Ok(query) => query,
        Err(_) => return pause(client, ctx, false).await,
    };

    let query = query.trim_matches(|c: char| c.is_whitespace() || c == '<' || c == '>');
    let queries = vec![
        query.to_owned(),
        format!("ytsearch:{}", query),
        format!("scsearch:{}", query),
    ];
    let node = client.get_node(guild_id).await?;
    let mut queue: Vec<Track> = Vec::new();
    for subquery in queries {
        if let Some(results) = load_tracks(client, &node, subquery.as_str()).await {
            queue = results
                .into_iter()
                .filter_map(|t| Track::try_from((user.clone(), t)).ok())
                .collect();
            break;
        }
    }

    let duration = format_duration(queue.iter().map(|t| t.info.length).sum());
    let response = match queue.len() {
        0 => format!(":bulb: No results found for `{}`", query),
        1 => format!(
            ":notes: Added **[{}](<{}>)** ({}) to the music queue.",
            &queue[0].info, &queue[0].info.uri, duration
        ),
        x => format!(
            ":notes: Added **{}** tracks ({}) to the music queue.",
            x, duration
        ),
    };

    if queue.len() > 0 {
        if let Some(mut state) = client.states.get_mut(&guild_id) {
            state.value_mut().queue.extend(user.id, queue);
        } else {
            client.connect(guild_id, channel_id).await?;
            let mut state_queue = MusicQueue::new();
            state_queue.extend(user.id, queue);
            client.states.insert(
                guild_id,
                PlayerState {
                    skip_votes: HashSet::new(),
                    queue: state_queue,
                    now_playing_ui: None,
                    queue_ui: None,
                    now_playing_ui_slash: None,
                    queue_ui_slash: None,
                },
            );
            client.start_playing(guild_id).await?;
        }
    }

    Ok(Response::direct().content(&response))
}

async fn pause(client: &Client<'static>, ctx: &CommandContext, pause: bool) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    get_player!(client, &guild_id).set_pause(pause)?;
    let response = if pause {
        "The music bot has been paused."
    } else {
        "The music bot has been unpaused."
    };
    Ok(Response::direct().content(response))
}

async fn stop(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    client.disconnect(guild_id).await?;
    Ok(Response::direct().content("The player has been stopped and the queue has been cleared"))
}

async fn skip(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    if ctx.get_flag("force").unwrap_or(false) {
        return forceskip(client, ctx).await;
    }
    let guild_id = require_playing(client, ctx)?;
    let user_id = ctx.user().id;
    require_in_voice_channel(client, ctx).await?;
    let listeners = client.count_listeners(guild_id).await?;
    let (votes, required, requestor) = client
        .mutate_state(guild_id, |state| {
            let (requestor, _) = state.currently_playing().unwrap();
            state.skip_votes.insert(user_id);
            (state.skip_votes.len(), listeners / 2, requestor)
        })
        .unwrap();

    let response = if votes >= required || requestor == user_id {
        format!("Skipped `{}`", client.play_next(guild_id).await?.unwrap())
    } else {
        format!("Total votes: `{}/{}`.", votes, required)
    };

    Ok(Response::direct().content(&response))
}

async fn remove(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    if ctx.get_flag("all").unwrap_or(false) {
        return remove_all(&client, &ctx).await;
    }

    let guild_id = require_playing(client, &ctx)?;
    require_in_voice_channel(client, &ctx).await?;
    let user = ctx.user();
    let idx = ctx.get_int("position")?;

    if idx == 0 {
        // Do not allow removing the currently playing song from the queue.
        bail!(CommandError::InvalidArgument(
            "Cannot remove the currently playing song. Consider using `/skip`.".to_owned()
        ));
    } else if idx < 0 {
        bail!(CommandError::InvalidArgument(
            "Negative positions are not valid.".to_owned()
        ));
    }

    let idx = idx as usize;
    let config = client.get_config(guild_id).await?;
    let dj = is_dj(&config, &ctx.command.member.as_ref().unwrap().roles);

    let response = client
        .mutate_state(guild_id, |state| {
            match state.queue.get(idx).map(|kv| kv.value.clone()) {
                Some(track) if user.id == track.requestor.id || dj => {
                    state.queue.remove(idx);
                    format!("Removed `{}` from the queue.", track.info)
                }
                Some(track) => {
                    format!(
                        "Only a DJ or {} tracks from the queue.",
                        track.requestor.display_name()
                    )
                }
                None => format!("There is no track at index {} in the queue.", idx),
            }
        })
        .unwrap();

    Ok(Response::direct().content(&response))
}

async fn remove_all(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_in_voice_channel(client, &ctx).await?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.clear_key(ctx.user().id) {
                format!("Removed **{}** tracks from the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    Ok(Response::direct().content(&response))
}

async fn shuffle(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_in_voice_channel(client, &ctx).await?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.shuffle(ctx.user().id) {
                format!("Shuffled **{}** tracks in the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    Ok(Response::direct().content(&response))
}

async fn forceskip(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, &ctx).await?;
    let response = if let Some(previous) = client.play_next(guild_id).await? {
        format!("Skipped `{}`.", previous)
    } else {
        "There is nothing in the queue right now.".to_owned()
    };
    Ok(Response::direct().content(&response))
}

async fn volume(client: &Client<'static>, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    let volume = ctx.get_int("volume");
    let response = if let Ok(vol) = volume {
        require_dj(client, &ctx).await?;
        if vol < 0 || vol > 150 {
            bail!(CommandError::InvalidArgument(
                "Volume must be between 0 and 150.".into()
            ));
        }
        get_player!(client, &guild_id).set_volume(vol as u32)?;

        // Update config
        let mut config = client.get_config(guild_id).await?;
        config.set_volume(vol as u32);
        client.set_config(guild_id, config).await?;

        format!("Set volume to `{}`.", vol)
    } else {
        format!(
            "Current volume is `{}`.",
            get_player!(client, &guild_id).volume()
        )
    };
    Ok(Response::direct().content(&response))
}

async fn queue(client: &Client<'static>, ctx: CommandContext) -> Result<()> {
    let guild_id = ctx.guild_id()?;
    let ui = EmbedUI::<QueueUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .now_playing_ui_slash
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}

async fn now_playing(client: &Client<'static>, ctx: CommandContext) -> Result<()> {
    let guild_id = ctx.guild_id()?;
    let ui = EmbedUI::<NowPlayingUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .queue_ui_slash
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}
