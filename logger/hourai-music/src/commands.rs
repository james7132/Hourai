use crate::prelude::*;
use crate::{player::PlayerState, queue::MusicQueue, track::Track, ui::*, Client};
use anyhow::{bail, Result};
use hourai::{
    commands::{self, precondition::*, prelude::*, CommandError},
    models::{
        channel::Message,
        id::{ChannelId, GuildId},
        user::User,
        UserLike,
    },
};
use std::{collections::HashSet, convert::TryFrom};
use twilight_command_parser::{Arguments, Command};
use twilight_lavalink::http::LoadType;

macro_rules! get_player {
    ($client:expr, $guild_id: expr) => {
        $client.lavalink.players().get($guild_id).unwrap().value()
    };
}

pub async fn on_message_create(client: Client<'static>, evt: Message) -> Result<()> {
    if let Some(command) = client.parser.parse(evt.content.as_str()) {
        let ctx = commands::Context {
            message: &evt,
            http: client.http_client.clone(),
            cache: client.cache.clone(),
        };

        let result = match command {
            Command {
                name: "play",
                arguments,
                ..
            } => play(&client, ctx, arguments.into_remainder()).await,
            Command { name: "pause", .. } => pause(&client, ctx, true).await,
            Command { name: "stop", .. } => stop(&client, ctx).await,
            Command {
                name: "shuffle", ..
            } => shuffle(&client, ctx).await,
            Command { name: "skip", .. } => skip(&client, ctx).await,
            Command {
                name: "forceskip", ..
            } => forceskip(&client, ctx).await,
            Command {
                name: "remove",
                mut arguments,
                ..
            } => remove(&client, ctx, &mut arguments).await,
            Command {
                name: "removeall", ..
            } => remove_all(&client, ctx).await,
            Command {
                name: "nowplaying", ..
            } => now_playing(&client, ctx).await,
            Command { name: "np", .. } => now_playing(&client, ctx).await,
            Command { name: "queue", .. } => queue(&client, ctx).await,
            Command {
                name: "volume",
                mut arguments,
                ..
            } => volume(&client, ctx, &mut arguments).await,
            _ => {
                debug!("Failed to find command: {}", evt.content.as_str());
                Ok(())
            }
        };

        if let Err(err) = result {
            match err.downcast::<CommandError>() {
                Ok(command_error) => {
                    client
                        .http_client
                        .create_message(evt.channel_id)
                        .reply(evt.id)
                        .content(format!(":x: {}", command_error))?
                        .await?;
                }
                Err(err) => bail!(err),
            }
        }
    }
    Ok(())
}

fn require_in_voice_channel(
    client: &Client<'static>,
    ctx: &commands::Context<'_>,
) -> Result<(GuildId, ChannelId)> {
    let guild_id = require_in_guild(&ctx)?;

    let user = client.cache.voice_state(guild_id, ctx.message.author.id);
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

fn require_playing(client: &Client<'static>, ctx: &commands::Context<'_>) -> Result<GuildId> {
    let guild_id = require_in_guild(&ctx)?;
    client
        .states
        .get(&guild_id)
        .filter(|state| state.value().is_playing())
        .ok_or_else(|| CommandError::FailedPrecondition("No music is currently playing."))?;
    Ok(guild_id)
}

/// Requires that the author is a DJ on the server to use the command.
async fn require_dj(client: &Client<'static>, ctx: &commands::Context<'_>) -> Result<()> {
    let (guild_id, _) = require_in_voice_channel(client, &ctx)?;
    if let Some(member) = &ctx.message.member {
        let config = client.get_config(guild_id).await?;
        let dj_roles = config.get_dj_role_id();
        for role_id in member.roles.iter() {
            if dj_roles.contains(&role_id.0) {
                return Ok(());
            }
        }
    }
    bail!(CommandError::FailedPrecondition(
        "User must be a DJ to use this command."
    ))
}

async fn load_tracks(
    client: &Client<'static>,
    author: &User,
    node: &Node,
    query: &str,
) -> Vec<Track> {
    let response = match client.load_tracks(node, query).await {
        Ok(tracks) => tracks,
        Err(_) => return vec![],
    };

    let tracks = match response {
        LoadedTracks {
            load_type: LoadType::TrackLoaded,
            tracks,
            ..
        } => {
            assert!(tracks.len() > 0);
            vec![tracks[0].clone()]
        }
        LoadedTracks {
            load_type: LoadType::SearchResult,
            tracks,
            ..
        } => {
            // TODO(james7132): This could be improved by doing a edit distance from
            // the query for similarity matching.
            assert!(tracks.len() > 0);
            vec![tracks[0].clone()]
        }
        LoadedTracks {
            load_type: LoadType::PlaylistLoaded,
            tracks,
            playlist_info,
            ..
        } => {
            if let Some(idx) = playlist_info.selected_track {
                vec![tracks[idx as usize].clone()]
            } else {
                tracks
            }
        }
        LoadedTracks {
            load_type: LoadType::LoadFailed,
            ..
        } => {
            error!("Failed to load query `{}`", query);
            vec![]
        }
        _ => vec![],
    };

    tracks
        .into_iter()
        .filter_map(|t| Track::try_from((author.clone(), t)).ok())
        .collect()
}

async fn play(
    client: &Client<'static>,
    ctx: commands::Context<'_>,
    query: Option<&str>,
) -> Result<()> {
    let (guild_id, channel_id) = require_in_voice_channel(client, &ctx)?;

    if query.is_none() {
        return pause(client, ctx, false).await;
    }

    let query = query
        .unwrap()
        .trim_matches(|c: char| c.is_whitespace() || c == '<' || c == '>');
    let queries = vec![
        query.to_owned(),
        format!("ytsearch:{}", query),
        format!("scsearch:{}", query),
    ];
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
        format!(
            ":notes: Added **{}** tracks ({}) to the music queue.",
            queue.len(),
            duration
        )
    } else if queue.len() == 1 {
        format!(
            ":notes: Added `{}` ({}) to the music queue.",
            &queue[0].info, duration
        )
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
            client.states.insert(
                guild_id,
                PlayerState {
                    skip_votes: HashSet::new(),
                    queue: state_queue,
                    now_playing_ui: None,
                    queue_ui: None,
                },
            );
            client.start_playing(guild_id).await?;
        }
    }

    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn pause(client: &Client<'static>, ctx: commands::Context<'_>, pause: bool) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_dj(client, &ctx).await?;
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
    let guild_id = require_playing(client, &ctx)?;
    require_dj(client, &ctx).await?;
    client.disconnect(guild_id).await?;
    ctx.respond()
        .content("The player has been stopped and the queue has been cleared")?
        .await?;
    Ok(())
}

async fn skip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_in_voice_channel(client, &ctx)?;
    let (votes, required) = client
        .mutate_state(guild_id, |state| {
            state.skip_votes.insert(ctx.message.author.id);
            let listeners = client.count_listeners(guild_id);
            (state.skip_votes.len(), listeners / 2)
        })
        .unwrap();

    let response = if votes >= required {
        format!("Skipped `{}`", client.play_next(guild_id).await?.unwrap())
    } else {
        format!("Total votes: `{}/{}`.", votes, required)
    };

    ctx.respond().content(response)?.await?;
    Ok(())
}

// TODO(james7132): Properly implmement
async fn remove(
    client: &Client<'static>,
    ctx: commands::Context<'_>,
    arguments: &mut Arguments<'_>,
) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_in_voice_channel(client, &ctx)?;
    let idx = arguments.parse_next::<usize>()?;
    commands::precondition::no_excess_arguments(arguments)?;

    if idx == 0 {
        // Do not allow removing the currently playing song from the queue.
        ctx.respond()
            .content(":x: Invalid index for removal.")?
            .await?;
        return Ok(());
    }

    let author = ctx.message.author.id;
    let response = client
        .mutate_state(guild_id, |state| {
            match state.queue.get(idx).map(|kv| kv.value.clone()) {
                Some(track) if author == track.requestor.id => {
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

    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn remove_all(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_in_voice_channel(client, &ctx)?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.clear_key(ctx.message.author.id) {
                format!("Removed **{}** tracks from the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn shuffle(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_in_voice_channel(client, &ctx)?;
    let response = client
        .mutate_state(guild_id, |state| {
            if let Some(count) = state.queue.shuffle(ctx.message.author.id) {
                format!("Shuffled **{}** tracks in the queue.", count)
            } else {
                "You currently do not have any tracks in the queue.".to_owned()
            }
        })
        .unwrap();
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn forceskip(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    require_dj(client, &ctx).await?;
    let response = if let Some(previous) = client.play_next(guild_id).await? {
        format!("Skipped `{}`.", previous)
    } else {
        "There is nothing in the queue right now.".to_owned()
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn volume(
    client: &Client<'static>,
    ctx: commands::Context<'_>,
    arguments: &mut Arguments<'_>,
) -> Result<()> {
    let guild_id = require_playing(client, &ctx)?;
    let volume = arguments.parse_next_opt::<i64>();
    commands::precondition::no_excess_arguments(arguments)?;
    let response = if let Some(vol) = volume {
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
            get_player!(client, &guild_id).volume_ref()
        )
    };
    ctx.respond().content(response)?.await?;
    Ok(())
}

async fn queue(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    let ui = EmbedUI::<QueueUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .now_playing_ui
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}

async fn now_playing(client: &Client<'static>, ctx: commands::Context<'_>) -> Result<()> {
    let guild_id = require_in_guild(&ctx)?;
    let ui = EmbedUI::<NowPlayingUI>::create(client.clone(), ctx).await?;
    client.mutate_state(guild_id, move |state| {
        state
            .queue_ui
            .replace(MessageUI::run(ui, Duration::from_secs(5)));
    });
    Ok(())
}
