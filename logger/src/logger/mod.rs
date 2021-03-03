use crate::prelude::*;
use crate::{init, db};
use crate::error::Result;
use crate::cache::{InMemoryCache, ResourceType};
use futures::stream::StreamExt;
use twilight_model::{
    user::User,
    guild::Permissions,
    guild::member::Member,
    gateway::{payload::*, presence::*},
};
use twilight_gateway::{
    Intents, Event, EventType, EventTypeFlags,
    cluster::*,
};
use mobc_redis::redis::aio::Connection;
use core::time::Duration;

const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::DIRECT_MESSAGES.bits() |
    Intents::GUILDS.bits() |
    Intents::GUILD_BANS.bits() |
    Intents::GUILD_MESSAGES.bits() |
    Intents::GUILD_MEMBERS.bits() |
    Intents::GUILD_PRESENCES.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::READY.bits() |
        EventTypeFlags::BAN_ADD.bits() |
        EventTypeFlags::BAN_REMOVE.bits() |
        EventTypeFlags::MEMBER_ADD.bits() |
        EventTypeFlags::MEMBER_CHUNK.bits() |
        EventTypeFlags::MEMBER_REMOVE.bits() |
        EventTypeFlags::MEMBER_UPDATE.bits() |
        EventTypeFlags::GUILD_CREATE.bits() |
        EventTypeFlags::GUILD_UPDATE.bits() |
        EventTypeFlags::GUILD_DELETE.bits() |
        EventTypeFlags::PRESENCE_UPDATE.bits() |
        EventTypeFlags::ROLE_CREATE.bits() |
        EventTypeFlags::ROLE_UPDATE.bits() |
        EventTypeFlags::ROLE_DELETE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(
        ResourceType::ROLE.bits() |
        ResourceType::GUILD.bits() |
        ResourceType::PRESENCE.bits() |
        ResourceType::USER_CURRENT.bits());

fn get_user_id(user: UserOrId) -> UserId {
    match user {
        UserOrId::User(user) => user.id,
        UserOrId::UserId { id } => id,
    }
}

pub async fn run(initializer: init::Initializer) {
    Client::new(initializer).await.run().await;
}

#[derive(Clone)]
struct Client {
    pub http_client: twilight_http::Client,
    pub gateway: Cluster,
    pub cache: InMemoryCache,
    pub sql: sqlx::PgPool,
    pub redis: db::RedisPool,
}

impl Client {

    pub async fn new(initializer: init::Initializer) -> Self {
        let config = initializer.config();
        let http_client = initializer.http_client();

        Self {
            http_client: http_client.clone(),
            gateway: Cluster::builder(&config.discord.bot_token, BOT_INTENTS)
                .shard_scheme(ShardScheme::Auto)
                .http_client(http_client)
                .build()
                .await
                .expect("Failed to connect to the Discord gateway"),
            cache: InMemoryCache::builder()
                .resource_types(CACHED_RESOURCES)
                .build(),
            sql: initializer.sql().await,
            redis: initializer.redis(),
        }
    }

    fn user_id(&self) -> UserId {
        self.cache.current_user().unwrap().id
    }

    pub async fn run(&self) {
        info!("Starting gateway...");
        self.gateway.up().await;
        info!("Client started.");

        let _ = tokio::spawn(self.clone().log_bans());
        let _ = tokio::spawn(self.clone().flush_online());

        let mut events = self.gateway.some_events(BOT_EVENTS);
        while let Some((shard_id, evt)) = events.next().await {
            self.cache.update(&evt);
            if evt.kind() != EventType::PresenceUpdate {
                tokio::spawn(self.clone().consume_event(shard_id, evt));
            }
        }

        info!("Shutting down gateway...");
        self.gateway.down();
        info!("Client stopped.");
    }

    async fn log_bans(self) {
        loop {
            info!("Refreshing bans...");
            for guild_id in self.cache.guilds() {
                if let Err(err) = self.refresh_bans(guild_id).await {
                    error!("Error while logging bans: {:?}", err);
                }
            }
            tokio::time::sleep(Duration::from_secs(180u64)).await;
        }
    }

    async fn flush_online(self) {
        loop {
            let mut pipeline = db::OnlineStatus::new();
            for guild_id in self.cache.guilds() {
                let presences = match self.cache.guild_online(guild_id) {
                    Some(p) => p,
                    _ => continue,
                };
                pipeline.set_online(guild_id, presences);
            }
            if let Ok(mut conn) = self.redis.get().await {
                let result = pipeline.build()
                                     .query_async::<Connection, ()>(&mut conn as &mut Connection)
                                     .await;
                if let Err(err) = result {
                    error!("Error while flushing statuses: {:?}", err);
                }
            }
            tokio::time::sleep(Duration::from_secs(60u64)).await;
        }
    }

    #[inline(always)]
    pub fn total_shards(&self) -> u64 {
        let shards = self.gateway.config().shard_config().shard()[1];
        assert!(shards > 0, "Bot somehow has a total of zero shards.");
        shards
    }

    /// Gets the shard ID for a guild.
    #[inline(always)]
    pub fn shard_id(&self, guild_id: GuildId) -> u64 {
        (guild_id.0 >> 22) % self.total_shards()
    }

    pub async fn chunk_guild(&self, guild_id: GuildId) -> Result<()> {
        debug!("Chunking guild: {}", guild_id);
        let request = RequestGuildMembers::builder(guild_id)
            .presences(true)
            .query(String::new(), None);
        self.gateway.command(self.shard_id(guild_id), &request).await?;
        Ok(())
    }

    pub async fn fetch_guild_permissions(
        &self,
        guild_id: GuildId,
        user_id: UserId
    ) -> Result<Permissions> {
        let local_member = db::Member::fetch(guild_id, user_id)
                                .fetch_one(&self.sql)
                                .await;
        if let Ok(member) = local_member {
            Ok(self.cache.guild_permissions(guild_id, user_id, member.role_ids()))
        } else {
            let roles = self.http_client
                .guild_member(guild_id, user_id)
                .await?
                .into_iter()
                .flat_map(|m| m.roles);
            Ok(self.cache.guild_permissions(guild_id, user_id, roles))
        }
    }

    async fn consume_event(self, shard_id: u64, event: Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::Ready(_) => self.on_shard_ready(shard_id).await,
            Event::BanAdd(evt) => self.on_ban_add(evt).await,
            Event::BanRemove(evt) => self.on_ban_remove(evt).await,
            Event::GuildCreate(evt) => self.on_guild_create(*evt).await,
            Event::GuildUpdate(evt) => Ok(()),
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.on_guild_leave(*evt).await
                } else {
                    Ok(())
                }
            },
            Event::MemberAdd(evt) => self.on_member_add(*evt).await,
            Event::MemberChunk(evt) => self.on_member_chunk(evt).await,
            Event::MemberRemove(evt) => self.on_member_remove(evt).await,
            Event::MemberUpdate(evt) => self.on_member_update(*evt).await,
            Event::RoleCreate(_) => Ok(()),
            Event::RoleUpdate(_) => Ok(()),
            Event::RoleDelete(evt) => self.on_role_delete(evt).await,
            _ => {
                error!("Unexpected event type: {:?}", event);
                Ok(())
            },
        };

        if let Err(err) = result {
            error!("Error while running event with {:?}: {:?}", kind, err);
        }
    }

    async fn on_shard_ready(self, shard_id: u64) -> Result<()> {
        db::Ban::clear_shard(shard_id, self.total_shards())
            .execute(&self.sql)
            .await?;
        Ok(())
    }

    async fn on_ban_add(self, evt: BanAdd) -> Result<()> {
        self.log_users(vec![evt.user.clone()]).await?;

        let perms = self.fetch_guild_permissions(evt.guild_id, self.user_id()).await?;
        if perms.contains(Permissions::BAN_MEMBERS) {
            if let Some(ban) = self.http_client.ban(evt.guild_id, evt.user.id).await? {
                db::Ban::from(evt.guild_id, ban)
                    .insert()
                    .execute(&self.sql)
                    .await?;
            }
        }

        Ok(())
    }

    async fn on_ban_remove(self, evt: BanRemove) -> Result<()> {
        futures::join!(
            self.log_users(vec![evt.user.clone()]),
            db::Ban::clear_ban(evt.guild_id, evt.user.id).execute(&self.sql)
        );
        Ok(())
    }

    async fn on_member_add(self, evt: MemberAdd) -> Result<()> {
        self.log_members(&vec![evt.0]).await?;
        Ok(())
    }

    async fn on_member_chunk(self, evt: MemberChunk) -> Result<()> {
        self.log_members(&evt.members).await?;
        Ok(())
    }

    async fn on_member_remove(self, evt: MemberRemove) -> Result<()> {
        self.log_users(vec![evt.user]).await?;
        Ok(())
    }

    async fn on_guild_unavailable(self, evt: GuildDelete) -> Result<()> {
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

        Ok(())
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> Result<()> {
        info!("Left guild {}", evt.id);
        futures::join!(
            db::Member::clear_guild(evt.id).execute(&self.sql),
            db::Ban::clear_guild(evt.id).execute(&self.sql)
        );
        Ok(())
    }

    async fn on_role_delete(self, evt: RoleDelete) -> Result<()> {
        let res = db::Member::clear_role(evt.guild_id, evt.role_id)
            .execute(&self.sql)
            .await;
        self.refresh_bans(evt.guild_id).await?;
        res?;
        Ok(())
    }

    async fn on_member_update(self, evt: MemberUpdate) -> Result<()> {
        if evt.user.bot {
            return Ok(());
        }

        db::Member {
            guild_id: evt.guild_id.0 as i64,
            user_id: evt.user.id.0 as i64,
            role_ids: evt.roles.iter().map(|id| id.0 as i64).collect(),
            nickname: evt.nick
        }.insert()
         .execute(&self.sql)
         .await?;
        db::Username::new(&evt.user)
            .insert()
            .execute(&self.sql)
            .await?;
        Ok(())
    }

    async fn log_users(&self, users: Vec<User>) -> Result<()> {
        let usernames = users.iter().map(|u| db::Username::new(u)).collect();
        db::Username::bulk_insert(usernames).execute(&self.sql).await?;
        Ok(())
    }

    async fn log_members(&self, members: &Vec<Member>) -> Result<()> {
        let usernames = members.iter().map(|m| db::Username::new(&m.user)).collect();

        db::Username::bulk_insert(usernames).execute(&self.sql).await?;
        let mut txn = self.sql.begin().await?;
        for member in members {
            db::Member::from(&member)
                .insert()
                .execute(&mut txn)
                .await?;
        }
        txn.commit().await?;
        Ok(())
    }

    async fn refresh_bans(&self, guild_id: GuildId) -> Result<()> {
        let perms = self.fetch_guild_permissions(guild_id, self.user_id()).await?;

        if perms.contains(Permissions::BAN_MEMBERS) {
            debug!("Fetching bans from guild {}", guild_id);
            let bans: Vec<db::Ban> = self.http_client.bans(guild_id).await?
                                         .into_iter().map(|b| db::Ban::from(guild_id, b))
                                         .collect();
            debug!("Fetched {} bans from guild {}", bans.len(), guild_id);
            let mut txn = self.sql.begin().await?;
            db::Ban::clear_guild(guild_id).execute(&mut txn).await?;
            db::Ban::bulk_insert(bans).execute(&mut txn).await?;
            txn.commit().await?;
        } else {
            debug!("Cleared bans from guild {}", guild_id);
            db::Ban::clear_guild(guild_id).execute(&self.sql).await?;
        }
        Ok(())
    }

}
