use crate::prelude::*;
use crate::{interaction_ui::*, player::PlayerState, queue::MusicQueue, track::Track, Client};
use anyhow::{bail, Result};
use hourai::{
    interactions::*,
    models::{
        id::{
            marker::{ChannelMarker, GuildMarker, RoleMarker},
            Id,
        },
        Snowflake,
    },
    proto::{guild_configs::MusicConfig, message_components::MusicButtonOption},
};
use std::cmp::Ordering;
use std::{collections::HashSet, convert::TryFrom};
use twilight_lavalink::http::LoadType;

macro_rules! get_player {
    ($client:expr, $guild_id: expr) => {
        $client.lavalink.players().get($guild_id).unwrap()
    };
}

pub async fn handle_command(client: Client, ctx: CommandContext) -> Result<()> {
    let result = match ctx.command() {
        Command::SubCommand("music", "play") => {
            ctx.defer().await?;
            play(&client, &ctx).await
        }
        Command::SubCommand("music", "pause") => {
            ctx.defer().await?;
            pause(&client, &ctx, Some(true)).await
        }
        Command::SubCommand("music", "unpause") => {
            ctx.defer().await?;
            pause(&client, &ctx, Some(false)).await
        }
        Command::SubCommand("music", "stop") => {
            ctx.defer().await?;
            stop(&client, &ctx).await
        }
        Command::SubCommand("music", "shuffle") => {
            ctx.defer().await?;
            shuffle(&client, &ctx).await
        }
        Command::SubCommand("music", "skip") => {
            ctx.defer().await?;
            if ctx.get_flag("force").unwrap_or(false) {
                forceskip(&client, &ctx).await
            } else {
                skip(&client, &ctx).await
            }
        }
        Command::SubCommand("music", "remove") => {
            ctx.defer().await?;
            remove(&client, &ctx).await
        }
        Command::SubCommand("music", "nowplaying") => {
            ctx.defer().await?;
            now_playing(&client, ctx).await?;
            return Ok(());
        }
        Command::SubCommand("music", "queue") => {
            ctx.defer().await?;
            queue(&client, ctx).await?;
            return Ok(());
        }
        Command::SubCommand("music", "volume") => {
            ctx.defer().await?;
            volume(&client, &ctx).await
        }
        _ => return Ok(()),
    };

    tracing::info!("Recieved command: {:?}", ctx.command);

    match result {
        Ok(response) => ctx.reply(response).await,
        Err(err) => {
            let response = Response::ephemeral();
            if let Some(command_err) = err.downcast_ref::<InteractionError>() {
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

pub async fn handle_component(client: Client, ctx: ComponentContext) -> Result<()> {
    let proto = ctx.metadata()?;
    let button = proto.get_music_button();
    match button.get_button_option() {
        MusicButtonOption::MUSIC_BUTTON_PLAY_PAUSE => {
            ctx.defer_update().await?;
            pause(&client, &ctx, None).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_STOP => {
            ctx.defer_update().await?;
            stop(&client, &ctx).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_NEXT_TRACK => {
            ctx.defer_update().await?;
            skip(&client, &ctx).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_QUEUE_NEXT_PAGE => {
            ctx.defer_update().await?;
            shift_queue_page(&client, &ctx, 1).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_QUEUE_PREV_PAGE => {
            ctx.defer_update().await?;
            shift_queue_page(&client, &ctx, -1).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_VOLUME_UP => {
            ctx.defer_update().await?;
            delta_volume(&client, &ctx, 5).await?;
        }
        MusicButtonOption::MUSIC_BUTTON_VOLUME_DOWN => {
            ctx.defer_update().await?;
            delta_volume(&client, &ctx, -5).await?;
        }
        _ => return Ok(()),
    }

    tracing::info!(
        "Recieved message component interactoin: {:?} {:?}",
        button.get_button_option(),
        ctx.component
    );

    Ok(())
}

async fn require_in_voice_channel(
    client: &Client,
    ctx: &impl InteractionContext,
) -> Result<(Id<GuildMarker>, Id<ChannelMarker>)> {
    let guild_id = ctx.guild_id()?;
    let user_id = ctx.user().id;
    let user: Option<Id<ChannelMarker>> = client
        .redis
        .guild(guild_id)
        .voice_states()
        .get_channel(user_id)
        .await?;
    let bot = client.get_channel(guild_id);
    if bot.is_some() && user != bot {
        bail!(InteractionError::FailedPrecondition(
            "You must be in the same voice channel to play music."
        ));
    } else if let Some(channel) = user {
        Ok((guild_id, channel))
    } else {
        bail!(InteractionError::FailedPrecondition(
            "You must be in a voice channel to play music."
        ));
    }
}

fn require_playing(client: &Client, ctx: &impl InteractionContext) -> Result<Id<GuildMarker>> {
    let guild_id = ctx.guild_id()?;
    client
        .states
        .get(&guild_id)
        .filter(|state| state.value().is_playing())
        .ok_or(InteractionError::FailedPrecondition(
            "No music is currently playing.",
        ))?;
    Ok(guild_id)
}

fn is_dj(config: &MusicConfig, roles: &[Id<RoleMarker>]) -> bool {
    let dj_roles = config.get_dj_role_id();
    roles.iter().any(|id| dj_roles.contains(&id.get()))
}

/// Requires that the author is a DJ on the server to use the command.
async fn require_dj(client: &Client, ctx: &impl InteractionContext) -> Result<()> {
    let _guild_id = ctx.guild_id()?;
    let _user_id = ctx.user().id;
    let (guild_id, _) = require_in_voice_channel(client, ctx).await?;
    let config = client.get_config(guild_id).await?;
    if let Some(member) = ctx.member() {
        if is_dj(&config, &member.roles) {
            return Ok(());
        }
    }
    bail!(InteractionError::FailedPrecondition(
        "User must be a DJ to use this command."
    ))
}

async fn load_tracks(
    client: &Client,
    node: &Node,
    query: &str,
) -> Option<Vec<twilight_lavalink::http::Track>> {
    let response = match client.load_tracks(node, query).await {
        Ok(tracks) => tracks,
        Err(err) => {
            tracing::error!(
                "Error while loading tracks for query {}: {} ({:?}",
                query,
                err,
                err
            );
            return None;
        }
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
            .unwrap_or(tracks),
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

async fn play(client: &Client, ctx: &CommandContext) -> Result<Response> {
    let (guild_id, channel_id) = require_in_voice_channel(client, ctx).await?;
    let user = ctx.user();
    let query = ctx.get_string("query")?;

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

    if !queue.is_empty() {
        if let Some(mut state) = client.states.get_mut(&guild_id) {
            state.value_mut().queue.extend(user.id, queue);
        } else {
            client.connect(guild_id, channel_id).await?;
            let mut state_queue = MusicQueue::default();
            state_queue.extend(user.id, queue);
            client.states.insert(
                guild_id,
                PlayerState {
                    skip_votes: HashSet::new(),
                    queue: state_queue,
                    now_playing_ui: None,
                    queue_ui: None,
                    queue_page: 0,
                },
            );
            client.start_playing(guild_id, None).await?;
        }
    }
    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn pause(
    client: &Client,
    ctx: &impl InteractionContext,
    pause: Option<bool>,
) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    let pause = {
        let player = get_player!(client, &guild_id);
        let pause = pause.unwrap_or(!player.paused());
        player.set_pause(pause)?;
        pause
    };
    let response = if pause {
        "The music bot has been paused."
    } else {
        "The music bot has been unpaused."
    };
    client.save_state(guild_id).await?;
    Ok(Response::direct().content(response))
}

async fn stop(client: &Client, ctx: &impl InteractionContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    client.disconnect(guild_id).await?;
    client.save_state(guild_id).await?;
    Ok(Response::direct().content("The player has been stopped and the queue has been cleared"))
}

async fn skip(client: &Client, ctx: &impl InteractionContext) -> Result<Response> {
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

    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn remove(client: &Client, ctx: &CommandContext) -> Result<Response> {
    if ctx.get_flag("all").unwrap_or(false) {
        return remove_all(client, ctx).await;
    }

    let guild_id = require_playing(client, ctx)?;
    require_in_voice_channel(client, ctx).await?;
    let user = ctx.user();
    let idx = ctx.get_int("position")?;

    let idx = match idx.cmp(&0) {
        Ordering::Less => bail!(InteractionError::InvalidArgument(
            "Negative positions are not valid.".to_owned()
        )),
        Ordering::Equal => {
            // Do not allow removing the currently playing song from the queue.
            bail!(InteractionError::InvalidArgument(
                "Cannot remove the currently playing song. Consider using `/skip`.".to_owned()
            ));
        }
        Ordering::Greater => idx as usize,
    };

    let config = client.get_config(guild_id).await?;
    let dj = is_dj(&config, &ctx.command.member.as_ref().unwrap().roles);

    let response = client
        .mutate_state(guild_id, |state| {
            match state.queue.get(idx).map(|kv| kv.value.clone()) {
                Some(track) if user.id.get() == track.requestor.get_id() || dj => {
                    state.queue.remove(idx);
                    format!("Removed `{}` from the queue.", track.info)
                }
                Some(track) => {
                    format!(
                        "Only a DJ or <@{}> can remove that track from the queue.",
                        track.requestor.id()
                    )
                }
                None => format!("There is no track at index {} in the queue.", idx),
            }
        })
        .unwrap();

    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn remove_all(client: &Client, ctx: &impl InteractionContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_in_voice_channel(client, ctx).await?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.clear_key(ctx.user().id) {
                format!("Removed **{}** tracks from the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn shuffle(client: &Client, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_in_voice_channel(client, ctx).await?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.shuffle(ctx.user().id) {
                format!("Shuffled **{}** tracks in the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn forceskip(client: &Client, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    let response = if let Some(previous) = client.play_next(guild_id).await? {
        format!("Skipped `{}`.", previous)
    } else {
        "There is nothing in the queue right now.".to_owned()
    };
    client.save_state(guild_id).await?;
    Ok(Response::direct().content(&response))
}

async fn volume(client: &Client, ctx: &CommandContext) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    let volume = ctx.get_int("volume");
    let response = if let Ok(vol) = volume {
        require_dj(client, ctx).await?;
        if !(0..=150).contains(&vol) {
            bail!(InteractionError::InvalidArgument(
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
    Ok(Response::direct().content(response))
}

async fn delta_volume(
    client: &Client,
    ctx: &impl InteractionContext,
    change: i64,
) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    require_dj(client, ctx).await?;
    let mut config = client.get_config(guild_id).await?;
    let volume = {
        use std::cmp::{max, min};
        let player = get_player!(client, &guild_id);
        let volume = player.volume() as i64;
        let volume = min(150, max(0, volume + change)) as u32;
        player.set_volume(volume)?;
        volume
    };
    config.set_volume(volume as u32);
    client.set_config(guild_id, config).await?;

    Ok(Response::direct().content(format!("Set volume to `{}`.", volume)))
}

async fn shift_queue_page(
    client: &Client,
    ctx: &impl InteractionContext,
    change: i64,
) -> Result<Response> {
    let guild_id = require_playing(client, ctx)?;
    client.mutate_state(guild_id, move |state| {
        state.queue_page += change;
    });
    Ok(Response::direct().content("Changed page."))
}

async fn queue(client: &Client, ctx: CommandContext) -> Result<()> {
    let guild_id = ctx.guild_id()?;
    let ui = EmbedUI::<QueueUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .now_playing_ui
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}

async fn now_playing(client: &Client, ctx: CommandContext) -> Result<()> {
    let guild_id = ctx.guild_id()?;
    let ui = EmbedUI::<NowPlayingUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .queue_ui
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}
