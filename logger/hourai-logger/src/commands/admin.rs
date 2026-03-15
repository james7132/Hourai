use super::prelude::*;
use crate::utils;
use futures::{channel::mpsc, future, prelude::*};
use hourai::{
    http::request::AuditLogReason,
    models::{
        channel::message::Message,
        util::Timestamp,
        guild::Permissions,
        id::{
            marker::{ChannelMarker, MessageMarker},
            Id,
        },
        user::User,
    },
    proto::action::{Action, BanMember_Type, StatusType},
};
use regex::Regex;
use std::{
    sync::Arc,
    time::Duration,
    time::{SystemTime, UNIX_EPOCH},
};

const MAX_PRUNED_MESSAGES: usize = 2000;
const MAX_PRUNED_MESSAGES_PER_BATCH: usize = 100;

fn parse_duration(duration: &str) -> Result<Duration> {
    humantime::parse_duration(duration).map_err(|err| {
        anyhow::anyhow!(InteractionError::InvalidArgument(format!(
            "Cannot parse `{}` as a duration: {}",
            duration, err
        )))
    })
}

fn build_reason(action: &str, authorizer: &User, reason: Option<&String>) -> String {
    if let Some(reason) = reason {
        format!(
            "{} by {}#{:04} for: {}",
            action, authorizer.name, authorizer.discriminator, reason
        )
    } else {
        format!(
            "{} by {}#{:04}",
            action, authorizer.name, authorizer.discriminator
        )
    }
}

pub(super) async fn ban(ctx: &CommandContext, executor: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::BAN_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Ban Members"));
    }
    let soft = ctx.get_flag("soft").unwrap_or(false);
    let action = if soft { "Softbanned" } else { "Banned" };
    let authorizer = ctx.command.member.as_ref().expect("Command without user.");
    let storage = executor.storage();
    let authorizer_roles = storage
        .redis()
        .guild(guild_id)
        .role_set(&authorizer.roles)
        .await?;

    // TODO(james7132): Properly display the errors.
    let users: Vec<_> = ctx.all_users("user").collect();
    let mut errors = Vec::new();
    let mut base = Action::new();
    base.set_guild_id(guild_id.get());
    base.mut_ban().set_field_type(if soft {
        BanMember_Type::SOFTBAN
    } else {
        BanMember_Type::BAN
    });
    base.set_reason(build_reason(
        action,
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    ));
    if let Ok(duration) = ctx.get_string("duration") {
        base.set_duration(parse_duration(duration)?.as_secs());
    }

    for user_id in users.iter() {
        if let Some(member) = ctx.resolve_member(*user_id) {
            let roles = storage
                .redis()
                .guild(guild_id)
                .role_set(&member.roles)
                .await?;
            if roles >= authorizer_roles {
                errors.push(format!(
                    "{}: Has higher roles, not authorized to {}.",
                    user_id,
                    if soft { "softban" } else { "ban" }
                ));
                continue;
            }
        }

        let mut action = base.clone();
        action.set_user_id(user_id.get());
        if let Err(err) = executor.execute_action(&action).await {
            tracing::error!("Error while running /ban on {}: {}", user_id, err);
            errors.push(format!("{}: {}", user_id, err));
        }
    }

    Ok(Response::direct().content(format!("{} {} users.", action, users.len() - errors.len())))
}

pub(super) async fn timeout(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    const DAY_SECS: i64 = 86400;

    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MODERATE_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Moderate Members"));
    }

    let authorizer = ctx.command.member.as_ref().expect("Command without user.");
    let mut guild = storage.redis().guild(guild_id);
    let authorizer_roles = guild.role_set(&authorizer.roles).await?;
    let reason = build_reason(
        "Timed out",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    );

    let duration = if let Ok(duration) = ctx.get_string("duration") {
        parse_duration(duration)?.as_secs() as i64
    } else {
        // Default to 1 day
        DAY_SECS
    };

    if duration > 28 * DAY_SECS {
        anyhow::bail!(InteractionError::InvalidArgument(
            "Max duration for timeout is 28 days.".into()
        ));
    }

    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_secs() as i64;
    let expiration = Timestamp::from_secs(now + duration).unwrap();

    let members: Vec<_> = ctx.all_users("user").collect();
    let mut errors = Vec::new();
    for member_id in members.iter() {
        if let Some(member) = ctx.resolve_member(*member_id) {
            let roles = guild.role_set(&member.roles).await?;
            if roles >= authorizer_roles {
                errors.push(format!(
                    "{}: Has higher or equal roles, not authorized to time out.",
                    member_id
                ));
                continue;
            }
        }

        let request = ctx
            .http
            .update_guild_member(guild_id, *member_id)
            .communication_disabled_until(Some(expiration.clone()))
            .unwrap()
            .reason(&reason)
            .unwrap();
        if let Err(err) = request.await {
            tracing::error!("Error while running /timeout on {}: {}", member_id, err);
            errors.push(format!("{}: {}", member_id, err));
        }
    }

    Ok(Response::direct().content(format!("Timed out {} users.", members.len() - errors.len())))
}

pub(super) async fn kick(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::KICK_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Kick Members"));
    }

    let authorizer = ctx.command.member.as_ref().expect("Command without user.");
    let mut guild = storage.redis().guild(guild_id);
    let authorizer_roles = guild.role_set(&authorizer.roles).await?;
    let reason = build_reason(
        "Kicked",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    );

    let members: Vec<_> = ctx.all_users("user").collect();
    let mut errors = Vec::new();
    for member_id in members.iter() {
        if let Some(member) = ctx.resolve_member(*member_id) {
            let roles = guild.role_set(&member.roles).await?;
            if roles >= authorizer_roles {
                errors.push(format!(
                    "{}: Has higher or equal roles, not authorized to kick.",
                    member_id
                ));
                continue;
            }
        }

        let request = ctx
            .http
            .remove_guild_member(guild_id, *member_id)
            .reason(&reason)
            .unwrap();
        if let Err(err) = request.await {
            tracing::error!("Error while running /kick on {}: {}", member_id, err);
            errors.push(format!("{}: {}", member_id, err));
        }
    }

    Ok(Response::direct().content(format!("Kicked {} users.", members.len() - errors.len())))
}

pub(super) async fn change_role(
    ctx: &CommandContext,
    executor: &ActionExecutor,
    status: StatusType,
) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MANAGE_ROLES) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Roles"));
    }

    let authorizer = ctx.member().expect("Command without user.");
    let members: Vec<_> = ctx.all_users("user").collect();
    let mut base = Action::new();
    base.set_guild_id(guild_id.get());
    base.mut_change_role()
        .mut_role_ids()
        .push(ctx.get_role("role")?.get());
    base.mut_change_role().set_field_type(status);
    base.set_reason(build_reason(
        if let StatusType::APPLY = status {
            "Added role"
        } else {
            "Removed role"
        },
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    ));
    if let Ok(duration) = ctx.get_string("duration") {
        base.set_duration(parse_duration(duration)?.as_secs());
    }

    let mut errors = Vec::new();
    for member_id in members.iter() {
        let mut action = base.clone();
        action.set_user_id(member_id.get());
        if let Err(err) = executor.execute_action(&action).await {
            tracing::error!(
                "Error while running /role {{add/remove}} on {}: {}",
                member_id,
                err
            );
            errors.push(format!("{}: {}", member_id, err));
        }
    }

    Ok(Response::direct().content(format!(
        "{} {} users.",
        if let StatusType::APPLY = status {
            "Added role to"
        } else {
            "Removed role from"
        },
        members.len() - errors.len()
    )))
}

pub(super) async fn deafen(ctx: &CommandContext, executor: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::DEAFEN_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Deafen Members"));
    }

    let authorizer = ctx.member().expect("Command without user.");
    let members: Vec<_> = ctx.all_users("user").collect();
    let mut base = Action::new();
    base.set_guild_id(guild_id.get());
    base.mut_deafen().set_field_type(StatusType::APPLY);
    base.set_reason(build_reason(
        "Deafened",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    ));
    if let Ok(duration) = ctx.get_string("duration") {
        base.set_duration(parse_duration(duration)?.as_secs());
    }

    let mut errors = Vec::new();
    for member_id in members.iter() {
        let mut action = base.clone();
        action.set_user_id(member_id.get());
        if let Err(err) = executor.execute_action(&action).await {
            tracing::error!("Error while running /deafen on {}: {}", member_id, err);
            errors.push(format!("{}: {}", member_id, err));
        }
    }

    Ok(Response::direct().content(format!("Deafened {} users.", members.len() - errors.len())))
}

pub(super) async fn mute(ctx: &CommandContext, executor: &ActionExecutor) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MUTE_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Mute Members"));
    }

    let authorizer = ctx.member().expect("Command without user.");
    let members: Vec<_> = ctx.all_users("user").collect();
    let mut base = Action::new();
    base.set_guild_id(guild_id.get());
    base.mut_mute().set_field_type(StatusType::APPLY);
    base.set_reason(build_reason(
        "Muted",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    ));
    if let Ok(duration) = ctx.get_string("duration") {
        base.set_duration(parse_duration(duration)?.as_secs());
    }

    let mut errors = Vec::new();
    for member_id in members.iter() {
        let mut action = base.clone();
        action.set_user_id(member_id.get());
        if let Err(err) = executor.execute_action(&action).await {
            tracing::error!("Error while running /mute on {}: {}", member_id, err);
            errors.push(format!("{}: {}", member_id, err));
        }
    }

    Ok(Response::direct().content(format!("Muted {} users.", members.len() - errors.len())))
}

pub(super) async fn move_cmd(ctx: &CommandContext, storage: &Storage) -> Result<Response> {
    ctx.defer().await?;
    let guild_id = ctx.guild_id()?;
    if !ctx.has_user_permission(Permissions::MOVE_MEMBERS) {
        anyhow::bail!(InteractionError::MissingPermission("Move Members"));
    }

    let authorizer = ctx.member().expect("Command without user.");
    let reason = build_reason(
        "Moved",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    );

    let states = storage
        .redis()
        .guild(guild_id)
        .voice_states()
        .get_channels()
        .await?;

    let src = ctx.get_channel("src")?;
    let dst = ctx.get_channel("dst")?;

    let mut success = 0;
    let mut errors = Vec::new();
    for (user_id, channel_id) in states {
        if channel_id != src {
            continue;
        }
        let request = ctx
            .http
            .update_guild_member(guild_id, user_id)
            .channel_id(Some(dst))
            .reason(&reason)
            .unwrap();
        if let Err(err) = request.await {
            tracing::error!("Error while running /move on {}: {}", user_id, err);
            errors.push(format!("{}: {}", user_id, err));
        } else {
            success += 1;
        }
    }

    Ok(Response::direct().content(format!("Moved {} users.", success)))
}

async fn fetch_messages(
    channel_id: Id<ChannelMarker>,
    http: Arc<hourai::http::Client>,
    tx: mpsc::UnboundedSender<Message>,
) -> Result<()> {
    const TWO_WEEKS_SECS: u64 = 14 * 24 * 60 * 60;
    let limit = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_secs()
        - TWO_WEEKS_SECS;
    let mut oldest = None;
    loop {
        let fut = if let Some(oldest) = oldest {
            http.channel_messages(channel_id).before(oldest).await?
        } else {
            http.channel_messages(channel_id).await?
        };
        let messages = fut.model().await?;
        for message in messages {
            oldest = oldest
                .map(|oldest| oldest.min(message.id))
                .or(Some(message.id));
            if message.timestamp.as_secs() < limit as i64 {
                return Ok(());
            } else if let Err(_) = tx.unbounded_send(message) {
                return Ok(());
            }
        }
    }
}

pub(super) async fn prune(ctx: &CommandContext) -> Result<Response> {
    ctx.guild_id()?;
    ctx.defer().await?;
    let count = ctx.get_int("count").unwrap_or(100) as usize;
    if count > MAX_PRUNED_MESSAGES {
        anyhow::bail!(InteractionError::InvalidArgument(format!(
            "Prune only supports up to {} messages.",
            MAX_PRUNED_MESSAGES
        )));
    }

    let mut filters: Vec<Box<dyn Fn(&Message) -> bool + Send + 'static>> = Vec::new();
    let mine = ctx.get_flag("mine").unwrap_or(false);
    if mine {
        let user_id = ctx.user().id;
        filters.push(Box::new(move |msg| msg.author.id == user_id));
    }
    if ctx.get_flag("bot").unwrap_or(false) {
        filters.push(Box::new(|msg| msg.author.bot));
    }
    if ctx.get_flag("embed").unwrap_or(false) {
        filters.push(Box::new(|msg| {
            !msg.embeds.is_empty() || !msg.attachments.is_empty()
        }));
    }
    if ctx.get_flag("mention").unwrap_or(false) {
        filters.push(Box::new(|msg| {
            msg.mention_everyone || !msg.mention_roles.is_empty() || !msg.mentions.is_empty()
        }));
    }
    if let Ok(user) = ctx.get_user("user") {
        let user_id = user;
        filters.push(Box::new(move |msg| msg.author.id == user_id));
    }
    if let Ok(rgx) = ctx.get_string("match") {
        let regex = Regex::new(&rgx).map_err(|_| {
            InteractionError::InvalidArgument(
                "`match` must be a valid regex or pattern.".to_owned(),
            )
        })?;
        filters.push(Box::new(move |msg| regex.is_match(&msg.content)));
    }

    if !mine && !ctx.has_user_permission(Permissions::MANAGE_MESSAGES) {
        anyhow::bail!(InteractionError::MissingPermission("Manage Messages"));
    }

    let authorizer = ctx.member().expect("Command without user.");
    let reason = build_reason(
        "Pruned",
        authorizer.user.as_ref().unwrap(),
        ctx.get_string("reason").ok(),
    );

    let (tx, rx) = mpsc::unbounded();
    tokio::spawn(utils::log_error(
        "fetching messages to prune",
        fetch_messages(ctx.channel_id(), ctx.http().clone(), tx),
    ));

    let batches: Vec<Vec<Id<MessageMarker>>> = rx
        .skip(1)
        .take(count)
        .filter(move |msg| future::ready(filters.iter().all(|f| f(msg))))
        .map(|msg| msg.id)
        .chunks(MAX_PRUNED_MESSAGES_PER_BATCH)
        .map(|batch| Vec::from(batch))
        .collect()
        .await;

    let mut total = 0;
    for batch in batches {
        match batch.len() {
            0 => break,
            1 => {
                ctx.http()
                    .delete_message(ctx.channel_id(), batch[0])
                    .reason(&reason)?
                    .await?;
            }
            _ => {
                ctx.http()
                    .delete_messages(ctx.channel_id(), &batch)
                    .reason(&reason)?
                    .await?;
            }
        }
        total += batch.len();
    }
    Ok(Response::direct().content(format!("Pruned {} messages.", total)))
}
