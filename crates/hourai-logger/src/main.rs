#[macro_use]
extern crate lazy_static;

mod announcements;
mod auto;
mod buttons;
mod commands;
mod member_chunker;
mod message_filter;
mod message_logging;
mod pending_events;
mod roles;
mod utils;
mod verification;
mod web;

use anyhow::Result;
use core::time::Duration;
use hourai::{
    cache::{InMemoryCache, ResourceType},
    config::{self, HouraiConfig},
    gateway::{Event, EventType, EventTypeFlags, Intents, MessageSender, StreamExt as _},
    init,
    models::{
        application::interaction::{Interaction, InteractionType},
        channel::{Channel, ChannelType, Message},
        gateway::payload::incoming::*,
        guild::{Member, MemberFlags, Permissions, Role},
        http::interaction::*,
        id::{marker::*, Id},
        user::User,
        MessageLike,
    },
};
use hourai_redis::*;
use hourai_sql::{Ban, Executor, Username};
use hourai_storage::{actions::ActionExecutor, Storage};
use std::sync::Arc;
use tracing::{debug, error, info, warn};
use twilight_util::builder::embed::*;

const RESUME_KEY: &str = "LOGGER";
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits()
        | Intents::GUILD_MODERATION.bits()
        | Intents::GUILD_VOICE_STATES.bits()
        | Intents::GUILD_MEMBERS.bits()
        | Intents::GUILD_MESSAGES.bits()
        | Intents::GUILD_PRESENCES.bits()
        | Intents::MESSAGE_CONTENT.bits(),
);

fn main() -> Result<()> {
    let config = config::load_config(&config::get_config_path());
    init::init(&config);
    tokio::runtime::Builder::new_multi_thread()
        .enable_all()
        .build()?
        .block_on(async_main(config))
}

async fn async_main(config: HouraiConfig) -> Result<()> {
    let cache = InMemoryCache::builder()
        .resource_types(ResourceType::MEMBER | ResourceType::GUILD | ResourceType::PRESENCE)
        .build();

    let storage = Storage::init(&config).await;
    let http = Arc::new(init::http_client(&config));
    let shards: Vec<init::GatewayShard> = init::create_shards(&config, &http, BOT_INTENTS).await?;
    let mut senders = Vec::new();
    for shard in shards.iter() {
        senders.push(shard.sender());
    }
    let senders = Arc::new(senders);

    let current_user = http.current_user().await?.model().await?;
    let bot_app = http.current_user_application().await?.model().await?;

    info!(
        "Application ID: {}, User ID: {}",
        bot_app.id, current_user.id
    );

    let bot_user = User {
        accent_color: current_user.accent_color,
        avatar: current_user.avatar,
        avatar_decoration: None,
        avatar_decoration_data: None,
        banner: current_user.banner,
        bot: current_user.bot,
        discriminator: current_user.discriminator,
        email: current_user.email,
        flags: current_user.flags,
        global_name: None,
        id: current_user.id,
        locale: None,
        mfa_enabled: Some(current_user.mfa_enabled),
        name: current_user.name,
        premium_type: current_user.premium_type,
        public_flags: current_user.public_flags,
        system: None,
        verified: current_user.verified,
    };

    let actions = ActionExecutor::new(bot_user, http, storage.clone());

    let client = Client(Arc::new(ClientRef {
        gateway: senders.clone(),
        cache: cache.clone(),
        actions: actions.clone(),
        member_chunker: member_chunker::MemberChunker::new(senders.clone()),
    }));

    // Setup background tasks
    let server = web::run_server(
        config.clone(),
        storage.sql().clone(),
        storage.redis().clone(),
    )?;
    tokio::spawn(server);

    tokio::spawn(client.clone().log_bans());
    tokio::spawn(flush_online(cache.clone(), storage.redis().clone()));
    tokio::spawn(pending_events::run_pending_actions(actions.clone()));
    tokio::spawn(pending_events::run_pending_deescalations(actions.clone()));

    for mut shard in shards {
        let client = client.clone();
        let cache = cache.clone();
        tokio::spawn(async move {
            let shard_id = shard.id().number() as u64;
            while let Some(evt_res) = shard.next_event(EventTypeFlags::all()).await {
                if let Ok(evt) = evt_res {
                    if evt.kind() == EventType::PresenceUpdate {
                        cache.update(&evt);
                    } else {
                        client.pre_cache_event(&evt).await;
                        cache.update(&evt);
                        tokio::spawn(client.clone().consume_event(shard_id, evt));
                    }
                }
            }
        });
    }

    tokio::signal::ctrl_c().await?;
    info!("Shutting down gateway...");
    for sender in senders.iter() {
        sender.close(twilight_gateway::CloseFrame::NORMAL)?;
    }
    info!("Client stopped.");
    Ok(())
}

async fn flush_online(cache: InMemoryCache, redis: RedisClient) {
    let mut online_status = redis.online_status();
    loop {
        for guild_id in cache.guilds() {
            let presences = match cache.guild_online(guild_id) {
                Some(p) => p,
                _ => continue,
            };
            let result = online_status.set_online(guild_id, presences).await;
            if let Err(err) = result {
                error!("Error while flushing statuses: {} ({:?})", err, err);
            }
        }
        tokio::time::sleep(Duration::from_secs(60u64)).await;
    }
}

struct ClientRef {
    pub gateway: Arc<Vec<MessageSender>>,
    pub cache: InMemoryCache,
    pub actions: ActionExecutor,
    pub member_chunker: member_chunker::MemberChunker,
}

#[derive(Clone)]
pub struct Client(Arc<ClientRef>);

impl Client {
    async fn log_bans(self) {
        loop {
            tracing::info!("Refreshing bans...");
            let guilds = self.0.cache.guilds();
            self.log_all_bans(guilds.into_iter()).await;
            tracing::info!("Refreshed bans.");
            tokio::time::sleep(Duration::from_secs(3600)).await;
        }
    }

    async fn log_all_bans(&self, guilds: impl Iterator<Item = Id<GuildMarker>>) {
        for guild_id in guilds {
            if let Err(err) = self.refresh_bans(guild_id).await {
                error!("Error while logging bans: {} ({:?})", err, err);
            }
        }
    }

    #[inline(always)]
    pub fn total_shards(&self) -> u64 {
        self.0.gateway.len() as u64
    }

    /// Gets the shard ID for a guild.
    #[inline(always)]
    pub fn shard_id(&self, guild_id: Id<GuildMarker>) -> u64 {
        (guild_id.get() >> 22) % self.total_shards()
    }

    pub fn user_id(&self) -> Id<UserMarker> {
        self.0.actions.current_user().id
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        self.0.actions.storage()
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<hourai::http::Client> {
        self.0.actions.http()
    }

    pub async fn fetch_guild_permissions(
        &self,
        guild_id: Id<GuildMarker>,
        user_id: Id<UserMarker>,
    ) -> Result<Permissions> {
        let local_member = hourai_sql::Member::fetch(guild_id, user_id)
            .fetch_one(self.storage().sql())
            .await;
        let mut guild = self.storage().redis().guild(guild_id);
        if let Ok(member) = local_member {
            guild.guild_permissions(user_id, member.role_ids()).await
        } else {
            guild.guild_permissions(user_id, std::iter::empty()).await
        }
    }

    /// Handle events before the cache is updated.
    async fn pre_cache_event(&self, event: &Event) {
        let kind = event.kind();
        let result = match event {
            Event::MemberUpdate(ref evt) => {
                if self.0.cache.is_pending(evt.guild_id, evt.user.id) && !evt.pending {
                    let member = Member {
                        avatar: None,
                        communication_disabled_until: None,
                        deaf: false,
                        flags: MemberFlags::empty(),
                        joined_at: evt.joined_at,
                        // Unknown/dummy fields.
                        mute: false,
                        nick: evt.nick.clone(),
                        pending: false,
                        premium_since: evt.premium_since,
                        roles: evt.roles.clone(),
                        user: evt.user.clone(),
                    };
                    self.on_member_add(evt.guild_id, member).await
                } else {
                    Ok(())
                }
            }
            _ => Ok(()),
        };

        if let Err(err) = result {
            error!(
                "Error while running event with {:?}: {}, {}",
                kind, err, err
            );
        }
    }

    async fn consume_event(mut self, shard_id: u64, event: Event) {
        let kind = event.kind();
        let result = match event {
            Event::Ready(evt) => self.on_shard_ready(shard_id, *evt).await,
            Event::BanAdd(evt) => self.on_ban_add(evt).await,
            Event::BanRemove(evt) => self.on_ban_remove(evt).await,
            Event::GuildCreate(evt) => self.on_guild_create(*evt).await,
            Event::GuildUpdate(evt) => self.on_guild_update(*evt).await,
            Event::GuildDelete(evt) => {
                if !evt.unavailable.unwrap_or(false) {
                    self.on_guild_leave(evt).await
                } else {
                    Ok(())
                }
            }
            Event::InteractionCreate(evt) => self.on_interaction_create(evt.0).await,
            Event::MemberAdd(evt) => self.on_member_add(evt.guild_id, evt.member).await,
            Event::MemberChunk(evt) => self.on_member_chunk(evt).await,
            Event::MemberRemove(evt) => self.on_member_remove(evt).await,
            Event::MemberUpdate(evt) => self.on_member_update(*evt).await,
            Event::MessageCreate(evt) => self.on_message_create(evt.0).await,
            Event::MessageUpdate(evt) => self.on_message_update(*evt).await,
            Event::MessageDelete(evt) => self.on_message_delete(evt).await,
            Event::MessageDeleteBulk(evt) => self.on_message_bulk_delete(evt).await,
            Event::RoleCreate(evt) => self.on_role_create(evt).await,
            Event::RoleUpdate(evt) => self.on_role_update(evt).await,
            Event::RoleDelete(evt) => self.on_role_delete(evt).await,
            Event::ChannelCreate(evt) => self.on_channel_create(*evt).await,
            Event::ChannelUpdate(evt) => self.on_channel_update(*evt).await,
            Event::ChannelDelete(evt) => self.on_channel_delete(*evt).await,
            Event::ThreadCreate(evt) => self.on_thread_create(*evt).await,
            Event::ThreadListSync(evt) => self.on_thread_list_sync(evt).await,
            Event::VoiceStateUpdate(evt) => self.on_voice_state_update(*evt).await,
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            }
        };

        if let Err(err) = result {
            error!(
                "Error while running event with {:?}: {} ({:?})",
                kind, err, err
            );
        }
    }

    async fn on_shard_ready(self, shard_id: u64, evt: Ready) -> Result<()> {
        let sql = self.storage().sql();
        let (res1, res2) = futures::join!(
            sql.execute(Ban::clear_shard(shard_id, self.total_shards())),
            sql.execute(hourai_sql::Member::clear_present_shard(
                shard_id,
                self.total_shards()
            ))
        );

        self.log_all_bans(evt.guilds.into_iter().map(|g| g.id))
            .await;

        res1?;
        res2?;
        Ok(())
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> Result<()> {
        let sql = self.storage().sql();
        let (res1, res2) = futures::join!(
            sql.execute(hourai_sql::Member::clear_guild(evt.id)),
            sql.execute(hourai_sql::Ban::clear_guild(evt.id))
        );
        res1?;
        res2?;
        Ok(())
    }

    async fn on_ban_add(self, evt: BanAdd) -> Result<()> {
        let ban = Ban {
            guild_id: evt.guild_id.get() as i64,
            user_id: evt.user.id.get() as i64,
            reason: None,
            avatar: evt.user.avatar.map(|hash| hash.to_string()),
        }
        .insert();
        let (res1, res2) = futures::join!(
            self.storage().execute(ban),
            self.log_users(vec![evt.user.clone()]),
        );
        res1?;
        res2?;

        let redis = self.storage().redis();
        let config: hourai::proto::guild_configs::LoggingConfig =
            redis.guild(evt.guild_id).configs().get().await?;
        if config.has_modlog_channel_id() {
            let channel_id = Id::new(config.get_modlog_channel_id());
            let (mention, name) = (format!("<@{}>", evt.user.id), &evt.user.name);
            let embed = EmbedBuilder::new()
                .title("🔨 User Banned")
                .description(format!("**User:** {} ({})", mention, name))
                .color(0xED4245)
                .footer(EmbedFooterBuilder::new(format!("{:x}", evt.user.id.get())))
                .build();
            let _ = self
                .http()
                .create_message(channel_id)
                .embeds(&[embed])
                .await;
        }

        Ok(())
    }

    async fn on_ban_remove(self, evt: BanRemove) -> Result<()> {
        let (res1, res2) = futures::join!(
            self.storage()
                .execute(Ban::clear_ban(evt.guild_id, evt.user.id)),
            self.log_users(vec![evt.user.clone()])
        );
        res1?;
        res2?;
        Ok(())
    }

    async fn on_member_add(&self, guild_id: Id<GuildMarker>, member: Member) -> Result<()> {
        if !member.pending {
            let res = roles::on_member_join(self, guild_id, &member).await;
            let members = vec![member.clone()];
            self.log_members(&members).await?;
            res?;
        }
        if let Ok(config) = self
            .storage()
            .redis()
            .guild(guild_id)
            .configs()
            .get::<hourai::proto::auto_config::AutoConfig>()
            .await
        {
            let _ =
                auto::AutoEngine::on_member_join(&self.0.actions, &config, guild_id, &member).await;
        }
        let _ = verification::on_member_join(self, guild_id, &member).await;
        announcements::on_member_join(self, guild_id, member.user).await?;
        Ok(())
    }

    async fn on_member_chunk(&self, evt: MemberChunk) -> Result<()> {
        self.0.member_chunker.push_chunk(&evt);
        while let Err(err) = self.log_members(&evt.members).await {
            error!(
                "Error while chunking members, retrying: {} ({:?})",
                err, err
            );
        }
        Ok(())
    }

    async fn on_member_update(&self, evt: MemberUpdate) -> Result<()> {
        if evt.user.bot {
            return Ok(());
        }

        let mut txn = self.storage().sql().begin().await?;
        txn.execute(hourai_sql::Member::from(&evt).insert()).await?;
        txn.execute(Username::new(&evt.user).insert()).await?;
        txn.commit().await?;
        Ok(())
    }

    async fn on_member_remove(&self, evt: MemberRemove) -> Result<()> {
        let (res1, res2, res3) = futures::join!(
            self.storage().execute(hourai_sql::Member::set_present(
                evt.guild_id,
                evt.user.id,
                false
            )),
            self.log_users(vec![evt.user.clone()]),
            announcements::on_member_leave(self, evt.clone())
        );

        if let Ok(config) = self
            .storage()
            .redis()
            .guild(evt.guild_id)
            .configs()
            .get::<hourai::proto::auto_config::AutoConfig>()
            .await
        {
            let _ = auto::AutoEngine::on_member_remove(
                &self.0.actions,
                &config,
                evt.guild_id,
                &evt.user,
            )
            .await;
        }

        res1?;
        res2?;
        res3?;
        Ok(())
    }

    async fn on_channel_create(&self, evt: ChannelCreate) -> Result<()> {
        if let Some(guild_id) = evt.0.guild_id {
            self.storage()
                .redis()
                .guild(guild_id)
                .save_resource(evt.0.id, &evt.0)
                .await?;
        }
        Ok(())
    }

    async fn on_channel_update(&self, evt: ChannelUpdate) -> Result<()> {
        if let Some(guild_id) = evt.0.guild_id {
            self.storage()
                .redis()
                .guild(guild_id)
                .save_resource(evt.0.id, &evt.0)
                .await?;
        }
        Ok(())
    }

    async fn on_channel_delete(self, evt: ChannelDelete) -> Result<()> {
        if let Some(guild_id) = evt.0.guild_id {
            self.storage()
                .redis()
                .guild(guild_id)
                .delete_resource::<Channel>(evt.0.id)
                .await?;
        }
        Ok(())
    }

    async fn on_thread_create(&mut self, evt: ThreadCreate) -> Result<()> {
        if evt.0.kind == ChannelType::PublicThread {
            self.http().join_thread(evt.0.id).await?;
            info!("Joined thread {}", evt.0.id);
        }
        Ok(())
    }

    async fn on_thread_list_sync(&mut self, evt: ThreadListSync) -> Result<()> {
        for thread in evt.threads {
            if let Err(err) = self.http().join_thread(thread.id).await {
                error!(
                    "Error while joining new thread in guild {}: {} ({:?})",
                    evt.guild_id, err, err
                );
            } else {
                info!("Joined thread {}", thread.id);
            }
        }
        Ok(())
    }

    async fn on_message_create(self, evt: Message) -> Result<()> {
        if let Some(guild_id) = evt.guild_id {
            if let Ok(config) = self
                .storage()
                .redis()
                .guild(guild_id)
                .configs()
                .get::<hourai::proto::auto_config::AutoConfig>()
                .await
            {
                let _ = auto::AutoEngine::on_message(&self.0.actions, &config, &evt, false).await;
            }
        }
        match message_filter::check_message(&self.0.actions, &evt).await {
            Ok(deleted) => {
                if deleted {
                    return Ok(());
                }
            }
            Err(err) => {
                tracing::error!("Error while running message filter: {} ({:?})", err, evt);
            }
        }
        if !evt.author.bot {
            self.storage().redis().messages().cache(evt).await?;
        }
        Ok(())
    }

    async fn on_message_update(self, evt: MessageUpdate) -> Result<()> {
        // TODO(james7132): Figure this out
        //if message_filter::check_message(&self.0.actions, &evt).await? {
        //return Ok(());
        //}
        let mut messages = self.storage().redis().messages();
        let cached = messages.fetch(evt.channel_id, evt.id).await?;
        if let Some(mut msg) = cached {
            let before = msg.clone();
            msg.set_content(evt.content.clone());
            if let Some(guild_id) = msg.guild_id() {
                if let Ok(config) = self
                    .storage()
                    .redis()
                    .guild(guild_id)
                    .configs()
                    .get::<hourai::proto::auto_config::AutoConfig>()
                    .await
                {
                    let _ =
                        auto::AutoEngine::on_message(&self.0.actions, &config, &msg, true).await;
                }
            }
            tokio::spawn(message_logging::on_message_update(
                self.clone(),
                before.clone(),
                msg,
            ));
            messages.cache(before).await?;
        }

        Ok(())
    }

    async fn on_interaction_create(self, evt: Interaction) -> Result<()> {
        match evt.kind {
            InteractionType::Ping => {
                self.http()
                    .interaction(evt.application_id)
                    .create_response(
                        evt.id,
                        &evt.token,
                        &InteractionResponse {
                            kind: InteractionResponseType::Pong,
                            data: None,
                        },
                    )
                    .await?;
            }
            InteractionType::ApplicationCommand => {
                let ctx = hourai::interactions::CommandContext::new(self.http().clone(), evt);
                commands::handle_command(ctx, &self.0.actions).await?;
            }
            interaction => {
                warn!("Unknown incoming interaction: {:?}", interaction);
                return Ok(());
            }
        };
        Ok(())
    }

    async fn on_message_delete(mut self, evt: MessageDelete) -> Result<()> {
        message_logging::on_message_delete(&mut self, &evt).await?;
        self.storage()
            .redis()
            .messages()
            .delete(evt.channel_id, evt.id)
            .await?;
        Ok(())
    }

    async fn on_message_bulk_delete(self, evt: MessageDeleteBulk) -> Result<()> {
        tokio::spawn(message_logging::on_message_bulk_delete(
            self.clone(),
            evt.clone(),
        ));
        self.storage()
            .redis()
            .messages()
            .bulk_delete(evt.channel_id, evt.ids)
            .await?;
        Ok(())
    }

    async fn on_guild_create(self, evt: GuildCreate) -> Result<()> {
        match evt {
            GuildCreate::Available(guild) => {
                info!("Guild Available: {}", guild.id);
                self.0.member_chunker.push_guild(guild.id);
                let mut redis_guild = self.storage().redis().guild(guild.id);
                redis_guild.voice_states().update_guild(&guild).await?;
                redis_guild.save(&guild).await?;
            }
            GuildCreate::Unavailable(guild) => {
                info!("Joined Guild: {}", guild.id);
            }
        }
        Ok(())
    }

    async fn on_guild_update(self, evt: GuildUpdate) -> Result<()> {
        let mut redis_guild = self.storage().redis().guild(evt.0.id);
        redis_guild.save_resource(evt.0.id, &evt.0).await?;
        Ok(())
    }

    async fn on_role_create(self, evt: RoleCreate) -> Result<()> {
        self.storage()
            .redis()
            .guild(evt.guild_id)
            .save_resource(evt.role.id, &evt.role)
            .await?;
        Ok(())
    }

    async fn on_role_update(self, evt: RoleUpdate) -> Result<()> {
        self.storage()
            .redis()
            .guild(evt.guild_id)
            .save_resource(evt.role.id, &evt.role)
            .await?;
        Ok(())
    }

    async fn on_role_delete(self, evt: RoleDelete) -> Result<()> {
        self.storage()
            .redis()
            .guild(evt.guild_id)
            .delete_resource::<Role>(evt.role_id)
            .await?;
        Ok(())
    }

    async fn on_voice_state_update(self, evt: VoiceStateUpdate) -> Result<()> {
        let before = if let Some(guild_id) = evt.0.guild_id {
            self.storage()
                .redis()
                .guild(guild_id)
                .voice_states()
                .get_channel(evt.0.user_id)
                .await?
        } else {
            None
        };
        announcements::on_voice_update(&self, evt.0.clone(), before).await?;
        if let Some(guild_id) = evt.0.guild_id {
            let redis = self.storage().redis().guild(guild_id);
            redis.voice_states().save(&evt.0).await?;
        }
        Ok(())
    }

    pub async fn log_users(&self, users: Vec<User>) -> Result<()> {
        let mut txn = self.storage().sql().begin().await?;
        for user in users {
            txn.execute(Username::new(&user).insert()).await?;
        }
        txn.commit().await?;
        Ok(())
    }

    pub async fn log_members(&self, members: &[Member]) -> Result<()> {
        let mut txn = self.storage().sql().begin().await?;
        for member in members {
            txn.execute(Username::new(&member.user).insert()).await?;
            txn.execute(hourai_sql::Member::from(member).insert())
                .await?;
        }
        txn.commit().await?;
        Ok(())
    }

    async fn refresh_bans(&self, guild_id: Id<GuildMarker>) -> Result<()> {
        let perms = self
            .fetch_guild_permissions(guild_id, self.user_id())
            .await?;

        if perms.contains(Permissions::BAN_MEMBERS) {
            debug!("Fetching bans from guild {}", guild_id);
            let fetched_bans = self.http().bans(guild_id).await?.model().await?;
            let bans: Vec<Ban> = fetched_bans
                .iter()
                .map(|b| Ban {
                    guild_id: guild_id.get() as i64,
                    user_id: b.user.id.get() as i64,
                    reason: b.reason.clone(),
                    avatar: b.user.avatar.map(|hash| hash.to_string()),
                })
                .collect();
            debug!("Fetched {} bans from guild {}", bans.len(), guild_id);
            let mut txn = self.storage().sql().begin().await?;
            txn.execute(Ban::clear_guild(guild_id)).await?;
            txn.execute(Ban::bulk_insert(bans)).await?;
            txn.commit().await?;

            // Log user data from bans
            let users: Vec<User> = fetched_bans.into_iter().map(|ban| ban.user).collect();
            self.log_users(users).await?;
        } else {
            debug!("Cleared bans from guild {}", guild_id);
        }
        Ok(())
    }
}
