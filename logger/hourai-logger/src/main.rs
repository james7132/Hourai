#[macro_use]
extern crate lazy_static;

mod announcements;
mod commands;
mod listings;
mod message_filter;
mod message_logging;
mod pending_events;
mod roles;

use anyhow::Result;
use core::time::Duration;
use futures::stream::StreamExt;
use hourai::{
    cache::{InMemoryCache, ResourceType},
    config,
    gateway::{cluster::*, Event, EventType, EventTypeFlags, Intents},
    init,
    models::{
        application::{callback::InteractionResponse, interaction::Interaction},
        channel::{Channel, GuildChannel, Message},
        gateway::payload::{incoming::*, outgoing::RequestGuildMembers},
        guild::{member::Member, Permissions, Role},
        id::*,
        user::User,
    },
};
use hourai_redis::*;
use hourai_sql::{Ban, Executor, Username};
use hourai_storage::{actions::ActionExecutor, Storage};
use std::sync::Arc;
use tracing::{debug, error, info, warn};

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits()
        | Intents::GUILD_BANS.bits()
        | Intents::GUILD_MESSAGES.bits()
        | Intents::GUILD_MEMBERS.bits()
        | Intents::GUILD_PRESENCES.bits()
        | Intents::GUILD_VOICE_STATES.bits(),
);

const BOT_EVENTS: EventTypeFlags = EventTypeFlags::from_bits_truncate(
    EventTypeFlags::READY.bits()
        | EventTypeFlags::BAN_ADD.bits()
        | EventTypeFlags::BAN_REMOVE.bits()
        | EventTypeFlags::GUILD_CREATE.bits()
        | EventTypeFlags::GUILD_DELETE.bits()
        | EventTypeFlags::GUILD_UPDATE.bits()
        | EventTypeFlags::INTERACTION_CREATE.bits()
        | EventTypeFlags::MEMBER_ADD.bits()
        | EventTypeFlags::MEMBER_CHUNK.bits()
        | EventTypeFlags::MEMBER_REMOVE.bits()
        | EventTypeFlags::MEMBER_UPDATE.bits()
        | EventTypeFlags::MESSAGE_CREATE.bits()
        | EventTypeFlags::MESSAGE_DELETE.bits()
        | EventTypeFlags::MESSAGE_DELETE_BULK.bits()
        | EventTypeFlags::MESSAGE_UPDATE.bits()
        | EventTypeFlags::PRESENCE_UPDATE.bits()
        | EventTypeFlags::ROLE_CREATE.bits()
        | EventTypeFlags::ROLE_DELETE.bits()
        | EventTypeFlags::ROLE_UPDATE.bits()
        | EventTypeFlags::THREAD_CREATE.bits()
        | EventTypeFlags::THREAD_LIST_SYNC.bits()
        | EventTypeFlags::VOICE_STATE_UPDATE.bits(),
);

const CACHED_RESOURCES: ResourceType = ResourceType::from_bits_truncate(
    ResourceType::GUILD.bits() | ResourceType::MEMBER.bits() | ResourceType::PRESENCE.bits(),
);

#[tokio::main]
async fn main() {
    let config = config::load_config(config::get_config_path().as_ref());

    init::init(&config);
    let http_client = Arc::new(init::http_client(&config));
    let storage = Storage::init(&config).await;
    let cache = InMemoryCache::builder()
        .resource_types(CACHED_RESOURCES)
        .build();

    info!("Updating commands...");
    if let Err(err) = http_client
        .set_global_commands(&config.commands)
        .expect("Failed to create global commands")
        .exec()
        .await
    {
        warn!("Failed to update global commands: {:?}", err);
    }

    let (gateway, mut events) = init::cluster(&config, BOT_INTENTS)
        .shard_scheme(ShardScheme::Auto)
        .http_client(http_client.clone())
        .event_types(BOT_EVENTS)
        .build()
        .await
        .expect("Failed to connect to the Discord gateway");
    let gateway = Arc::new(gateway);

    let user = http_client
        .current_user()
        .exec()
        .await
        .expect("Current user should not fail to load.")
        .model()
        .await
        .expect("Failed to deserialize bot CurrentUser.");

    let user = http_client
        .user(user.id)
        .exec()
        .await
        .expect("User should not fail to load")
        .model()
        .await
        .expect("Failed to deserialize bot user.");

    let actions = ActionExecutor::new(user, http_client, storage.clone());

    let client = Client(Arc::new(ClientRef {
        gateway: gateway.clone(),
        cache: cache.clone(),
        actions: actions.clone(),
    }));

    info!("Starting gateway...");
    gateway.up().await;
    info!("Client started.");

    tokio::spawn(listings::run_push_listings(
        client.clone(),
        config.clone(),
        Duration::from_secs(300),
    ));

    // Setup background tasks
    tokio::spawn(client.clone().log_bans());
    tokio::spawn(flush_online(cache.clone(), storage.redis().clone()));
    tokio::spawn(pending_events::run_pending_actions(actions.clone()));
    tokio::spawn(pending_events::run_pending_deescalations(actions.clone()));

    while let Some((shard_id, evt)) = events.next().await {
        if evt.kind() == EventType::PresenceUpdate {
            cache.update(&evt);
        } else {
            client.pre_cache_event(&evt).await;
            cache.update(&evt);
            tokio::spawn(client.clone().consume_event(shard_id, evt));
        }
    }

    info!("Shutting down gateway...");
    gateway.down();
    info!("Client stopped.");
}

async fn flush_online(cache: InMemoryCache, mut redis: RedisPool) {
    loop {
        let mut pipeline = OnlineStatus::new();
        for guild_id in cache.guilds() {
            let presences = match cache.guild_online(guild_id) {
                Some(p) => p,
                _ => continue,
            };
            pipeline.set_online(guild_id, presences);
        }
        let result = pipeline
            .build()
            .query_async::<RedisPool, ()>(&mut redis)
            .await;
        if let Err(err) = result {
            error!("Error while flushing statuses: {:?}", err);
        }
        tokio::time::sleep(Duration::from_secs(60u64)).await;
    }
}

struct ClientRef {
    pub gateway: Arc<Cluster>,
    pub cache: InMemoryCache,
    pub actions: ActionExecutor,
}

#[derive(Clone)]
pub struct Client(Arc<ClientRef>);

impl Client {
    async fn log_bans(self) {
        loop {
            info!("Refreshing bans...");
            for guild_id in self.0.cache.guilds() {
                if let Err(err) = self.refresh_bans(guild_id).await {
                    error!("Error while logging bans: {:?}", err);
                }
            }
            tokio::time::sleep(Duration::from_secs(180u64)).await;
        }
    }

    #[inline(always)]
    pub fn total_shards(&self) -> u64 {
        self.0.gateway.shards().len() as u64
    }

    /// Gets the shard ID for a guild.
    #[inline(always)]
    pub fn shard_id(&self, guild_id: GuildId) -> u64 {
        (guild_id.get() >> 22) % self.total_shards()
    }

    pub fn user_id(&self) -> UserId {
        self.0.actions.current_user().id
    }

    #[inline(always)]
    pub fn storage(&self) -> &Storage {
        &self.0.actions.storage()
    }

    #[inline(always)]
    pub fn http(&self) -> &Arc<hourai::http::Client> {
        self.0.actions.http()
    }

    pub async fn chunk_guild(&self, guild_id: GuildId) -> Result<()> {
        debug!("Chunking guild: {}", guild_id);
        let request = RequestGuildMembers::builder(guild_id)
            .presences(true)
            .query(String::new(), None);
        self.0
            .gateway
            .command(self.shard_id(guild_id), &request)
            .await?;
        Ok(())
    }

    pub async fn fetch_guild_permissions(
        &self,
        guild_id: GuildId,
        user_id: UserId,
    ) -> Result<Permissions> {
        let local_member = hourai_sql::Member::fetch(guild_id, user_id)
            .fetch_one(self.storage().sql())
            .await;
        let mut redis = self.storage().redis().clone();
        if let Ok(member) = local_member {
            hourai_redis::CachedGuild::guild_permissions(
                guild_id,
                user_id,
                member.role_ids(),
                &mut redis,
            )
            .await
        } else {
            let roles = self
                .http()
                .guild_member(guild_id, user_id)
                .exec()
                .await?
                .model()
                .await?
                .roles;
            hourai_redis::CachedGuild::guild_permissions(
                guild_id,
                user_id,
                roles.into_iter(),
                &mut redis,
            )
            .await
        }
    }

    /// Handle events before the cache is updated.
    async fn pre_cache_event(&self, event: &Event) {
        let kind = event.kind();
        let result = match event {
            Event::MemberUpdate(ref evt) => {
                if self.0.cache.is_pending(evt.guild_id, evt.user.id) && !evt.pending {
                    let member = Member {
                        guild_id: evt.guild_id,
                        nick: evt.nick.clone(),
                        pending: false,
                        premium_since: evt.premium_since.clone(),
                        roles: evt.roles.clone(),
                        user: evt.user.clone(),
                        joined_at: Some(evt.joined_at.clone()),

                        // Unknown/dummy fields.
                        deaf: false,
                        mute: false,
                    };
                    self.on_member_add(member).await
                } else {
                    Ok(())
                }
            }
            _ => Ok(()),
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {}", kind, err);
        }
    }

    async fn consume_event(mut self, shard_id: u64, event: Event) {
        let kind = event.kind();
        let result = match event {
            Event::Ready(_) => self.on_shard_ready(shard_id).await,
            Event::BanAdd(evt) => self.on_ban_add(evt).await,
            Event::BanRemove(evt) => self.on_ban_remove(evt).await,
            Event::GuildCreate(evt) => self.on_guild_create(*evt).await,
            Event::GuildUpdate(evt) => self.on_guild_update(*evt).await,
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.on_guild_leave(*evt).await
                } else {
                    Ok(())
                }
            }
            Event::InteractionCreate(evt) => self.on_interaction_create(evt.0).await,
            Event::MemberAdd(evt) => self.on_member_add(evt.0).await,
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
            Event::ChannelCreate(evt) => self.on_channel_create(evt).await,
            Event::ChannelUpdate(evt) => self.on_channel_update(evt).await,
            Event::ChannelDelete(evt) => self.on_channel_delete(evt).await,
            Event::ThreadCreate(evt) => self.on_thread_create(evt).await,
            Event::ThreadListSync(evt) => self.on_thread_list_sync(evt).await,
            Event::VoiceStateUpdate(evt) => self.on_voice_state_update(*evt).await,
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            }
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {}", kind, err);
        }
    }

    async fn on_shard_ready(self, shard_id: u64) -> Result<()> {
        let sql = self.storage().sql();
        let (res1, res2) = futures::join!(
            sql.execute(Ban::clear_shard(shard_id, self.total_shards())),
            sql.execute(hourai_sql::Member::clear_present_shard(
                shard_id,
                self.total_shards()
            ))
        );

        res1?;
        res2?;

        Ok(())
    }

    async fn on_ban_add(self, evt: BanAdd) -> Result<()> {
        let (res1, res2) = futures::join!(
            self.log_users(vec![evt.user.clone()]),
            announcements::on_member_ban(&self, evt.clone())
        );

        let perms = self
            .fetch_guild_permissions(evt.guild_id, self.user_id())
            .await?;
        if perms.contains(Permissions::BAN_MEMBERS) {
            if let Ok(ban) = self
                .http()
                .ban(evt.guild_id, evt.user.id)
                .exec()
                .await?
                .model()
                .await
            {
                self.storage()
                    .sql()
                    .execute(Ban::from(evt.guild_id, ban).insert())
                    .await?;
            }
        }

        res1?;
        res2?;
        Ok(())
    }

    async fn on_ban_remove(self, evt: BanRemove) -> Result<()> {
        let (res1, res2) = futures::join!(
            self.log_users(vec![evt.user.clone()]),
            self.storage()
                .sql()
                .execute(Ban::clear_ban(evt.guild_id, evt.user.id)),
        );

        res1?;
        res2?;
        Ok(())
    }

    async fn on_member_add(&self, member: Member) -> Result<()> {
        if !member.pending {
            let res = roles::on_member_join(&self, &member).await;
            let members = vec![member.clone()];
            self.log_members(&members).await?;
            res?;
        }
        announcements::on_member_join(&self, member.guild_id, member.user).await?;
        Ok(())
    }

    async fn on_member_chunk(&self, evt: MemberChunk) -> Result<()> {
        while let Err(err) = self.log_members(&evt.members).await {
            error!("Error while chunking members, retrying: {:?}", err);
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
            announcements::on_member_leave(&self, evt)
        );
        res1?;
        res2?;
        res3?;
        Ok(())
    }

    async fn on_channel_create(&self, evt: ChannelCreate) -> Result<()> {
        if let Channel::Guild(ref ch) = evt.0 {
            if let Some(guild_id) = ch.guild_id() {
                let mut redis = self.storage().redis().clone();
                hourai_redis::CachedGuild::save_resource(guild_id, ch.id(), ch)
                    .query_async(&mut redis)
                    .await?;
            }
        }
        Ok(())
    }

    async fn on_channel_update(&self, evt: ChannelUpdate) -> Result<()> {
        if let Channel::Guild(ref ch) = evt.0 {
            if let Some(guild_id) = ch.guild_id() {
                let mut redis = self.storage().redis().clone();
                hourai_redis::CachedGuild::save_resource(guild_id, ch.id(), ch)
                    .query_async(&mut redis)
                    .await?;
            }
        }
        Ok(())
    }

    async fn on_channel_delete(self, evt: ChannelDelete) -> Result<()> {
        if let Channel::Guild(ref ch) = evt.0 {
            if let Some(guild_id) = ch.guild_id() {
                let mut redis = self.storage().redis().clone();
                hourai_redis::CachedGuild::delete_resource::<GuildChannel>(guild_id, ch.id())
                    .query_async(&mut redis)
                    .await?;
            }
        }
        Ok(())
    }

    async fn on_thread_create(&mut self, evt: ThreadCreate) -> Result<()> {
        self.http().join_thread(evt.0.id()).exec().await?;
        info!("Joined thread {}", evt.0.id());
        Ok(())
    }

    async fn on_thread_list_sync(&mut self, evt: ThreadListSync) -> Result<()> {
        for thread in evt.threads {
            if let Err(err) = self.http().join_thread(thread.id()).exec().await {
                error!(
                    "Error while joining new thread in guild {}: {}",
                    evt.guild_id, err
                );
            } else {
                info!("Joined thread {}", thread.id());
            }
        }
        Ok(())
    }

    async fn on_message_create(self, evt: Message) -> Result<()> {
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
            CachedMessage::new(evt)
                .flush()
                .query_async(&mut self.storage().redis().clone())
                .await?;
        }
        Ok(())
    }

    async fn on_message_update(self, evt: MessageUpdate) -> Result<()> {
        // TODO(james7132): Figure this out
        //if message_filter::check_message(&self.0.actions, &evt).await? {
        //return Ok(());
        //}
        let mut redis = self.storage().redis().clone();
        let cached = CachedMessage::fetch(evt.channel_id, evt.id, &mut redis).await?;
        if let Some(mut msg) = cached {
            if let Some(content) = evt.content {
                let before = msg.clone();
                msg.set_content(content);
                tokio::spawn(message_logging::on_message_update(
                    self.clone(),
                    before.clone(),
                    msg,
                ));
                CachedMessage::new(before)
                    .flush()
                    .query_async(&mut redis)
                    .await?;
            }
        }

        Ok(())
    }

    async fn on_interaction_create(self, evt: Interaction) -> Result<()> {
        info!("Recieved interaction: {:?}", evt);
        match evt {
            Interaction::Ping(ping) => {
                self.http()
                    .interaction_callback(ping.id, &ping.token, &InteractionResponse::Pong)
                    .exec()
                    .await?;
            }
            Interaction::ApplicationCommand(cmd) => {
                let ctx = hourai::interactions::CommandContext {
                    http: self.http().clone(),
                    command: cmd,
                };
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
        CachedMessage::delete(evt.channel_id, evt.id)
            .query_async(&mut self.storage().redis().clone())
            .await?;
        Ok(())
    }

    async fn on_message_bulk_delete(self, evt: MessageDeleteBulk) -> Result<()> {
        tokio::spawn(message_logging::on_message_bulk_delete(
            self.clone(),
            evt.clone(),
        ));
        CachedMessage::bulk_delete(evt.channel_id, evt.ids)
            .query_async(&mut self.storage().redis().clone())
            .await?;
        Ok(())
    }

    async fn on_guild_create(self, evt: GuildCreate) -> Result<()> {
        let guild = evt.0;

        if guild.unavailable {
            info!("Joined Guild: {}", guild.id);
        } else {
            info!("Guild Available: {}", guild.id);
        }

        self.chunk_guild(guild.id).await?;
        let mut redis = self.storage().redis().clone();
        hourai_redis::CachedGuild::save(&guild)
            .query_async(&mut redis)
            .await?;
        hourai_redis::CachedVoiceState::update_guild(&guild)
            .query_async(&mut redis)
            .await?;

        Ok(())
    }

    async fn on_guild_update(self, evt: GuildUpdate) -> Result<()> {
        hourai_redis::CachedGuild::save_resource(evt.0.id, evt.0.id, &evt.0)
            .query_async(&mut self.storage().clone())
            .await?;
        Ok(())
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> Result<()> {
        info!("Left guild {}", evt.id);
        let mut redis = self.storage().redis().clone();
        hourai_redis::CachedGuild::delete(evt.id)
            .query_async(&mut redis)
            .await?;
        hourai_redis::CachedVoiceState::clear_guild(evt.id)
            .query_async(&mut redis)
            .await?;
        let (res1, res2) = futures::join!(
            self.storage()
                .execute(hourai_sql::Member::clear_guild(evt.id)),
            self.storage().execute(Ban::clear_guild(evt.id)),
        );
        res1?;
        res2?;
        Ok(())
    }

    async fn on_role_create(self, evt: RoleCreate) -> Result<()> {
        hourai_redis::CachedGuild::save_resource(evt.guild_id, evt.role.id, &evt.role)
            .query_async(&mut self.storage().redis().clone())
            .await?;
        Ok(())
    }

    async fn on_role_update(self, evt: RoleUpdate) -> Result<()> {
        hourai_redis::CachedGuild::save_resource(evt.guild_id, evt.role.id, &evt.role)
            .query_async(&mut self.storage().redis().clone())
            .await?;
        Ok(())
    }

    async fn on_role_delete(self, evt: RoleDelete) -> Result<()> {
        let res = self
            .storage()
            .execute(hourai_sql::Member::clear_role(evt.guild_id, evt.role_id))
            .await;
        let res2 = hourai_redis::CachedGuild::delete_resource::<Role>(evt.guild_id, evt.role_id)
            .query_async(&mut self.storage().redis().clone())
            .await;
        self.refresh_bans(evt.guild_id).await?;
        res?;
        res2?;
        Ok(())
    }

    async fn on_voice_state_update(self, evt: VoiceStateUpdate) -> Result<()> {
        let guild_id = match evt.0.guild_id {
            Some(id) => id,
            None => return Ok(()),
        };
        let mut redis = self.storage().redis().clone();
        let channel_id: Option<u64> =
            hourai_redis::CachedVoiceState::get_channel(guild_id, evt.0.user_id)
                .query_async(&mut redis)
                .await?;
        let channel_id = channel_id.and_then(ChannelId::new);
        announcements::on_voice_update(&self, evt.0.clone(), channel_id).await?;
        hourai_redis::CachedVoiceState::save(&evt.0)
            .query_async(&mut redis)
            .await?;
        Ok(())
    }

    async fn log_users(&self, users: Vec<User>) -> Result<()> {
        let usernames = users.iter().map(|u| Username::new(u)).collect();
        self.storage()
            .execute(Username::bulk_insert(usernames))
            .await?;
        Ok(())
    }

    async fn log_members(&self, members: &[Member]) -> Result<()> {
        let usernames = members.iter().map(|m| Username::new(&m.user)).collect();

        self.storage()
            .execute(Username::bulk_insert(usernames))
            .await?;
        let mut txn = self.storage().sql().begin().await?;
        for member in members {
            txn.execute(hourai_sql::Member::from(member).insert())
                .await?;
        }
        txn.commit().await?;
        Ok(())
    }

    async fn refresh_bans(&self, guild_id: GuildId) -> Result<()> {
        let perms = self
            .fetch_guild_permissions(guild_id, self.user_id())
            .await?;

        if perms.contains(Permissions::BAN_MEMBERS) {
            debug!("Fetching bans from guild {}", guild_id);
            let fetched_bans = self.http().bans(guild_id).exec().await?.model().await?;
            let bans: Vec<Ban> = fetched_bans
                .clone()
                .into_iter()
                .map(|b| Ban::from(guild_id, b))
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
            self.storage().execute(Ban::clear_guild(guild_id)).await?;
        }
        Ok(())
    }
}
