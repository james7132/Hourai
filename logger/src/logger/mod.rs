use crate::{init, db};
use crate::error::Result;
use crate::config::HouraiConfig;
use crate::cache::{InMemoryCache, ResourceType};
use futures::stream::StreamExt;
use twilight_model::{
    id::*,
    user::User,
    guild::Permissions,
    guild::member::Member,
    gateway::{payload::*, presence::*},
};
use twilight_gateway::{
    Intents, Event, EventType, EventTypeFlags,
    cluster::*,
};
use tracing::{info, debug, error};

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

pub async fn run(config: HouraiConfig) {
    Client::new(&config).await.run().await;
}

#[derive(Clone)]
struct Client {
    pub http_client: twilight_http::Client,
    pub gateway: Cluster,
    pub standby: twilight_standby::Standby,
    pub cache: InMemoryCache,
    pub sql: sqlx::PgPool,
    pub redis: db::RedisPool,
}

impl Client {

    pub async fn new(config: &HouraiConfig) -> Self {
        let http_client = init::create_http_client(config);

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
            standby: twilight_standby::Standby::new(),
            sql: init::create_pg_pool(&config).await,
            redis: init::create_redis_pool(&config),
        }
    }

    fn user_id(&self) -> UserId {
        self.cache.current_user().unwrap().id
    }

    pub async fn run(&self) {
        info!("Starting gateway...");
        self.gateway.up().await;
        info!("Client started.");

        let mut events = self.gateway.some_events(BOT_EVENTS);
        while let Some((_, evt)) = events.next().await {
            self.cache.update(&evt);
            self.standby.process(&evt);
            if evt.kind() != EventType::PresenceUpdate {
                tokio::spawn(self.clone().consume_event(evt));
            }
        }

        info!("Shutting down gateway...");
        self.gateway.down();
        info!("Client stopped.");
    }

    /// Gets the shard ID for a guild.
    pub fn shard_id(&self, guild_id: GuildId) -> u64 {
        let total_shards = self.gateway.config().shard_config().shard()[1];
        assert!(total_shards > 0, "Bot somehow has a total of zero shards.");
        (guild_id.0 >> 22) % total_shards
    }

    pub async fn chunk_guild(&self, guild_id: GuildId) -> Result<()> {
        debug!("Chunking guild: {}", guild_id);
        let request = RequestGuildMembers::builder(guild_id)
            .presences(true)
            .query(String::new(), None);
        self.gateway.command(self.shard_id(guild_id), &request).await?;
        Ok(())
    }

    pub fn guild_permissions<T>(
        &self,
        guild_id: GuildId,
        role_ids: T) -> Permissions
        where T: Iterator<Item=RoleId>
    {
        // The everyone role ID is the same as the guild ID.
        let everyone_perms = self.cache.role(RoleId(guild_id.0))
            .map(|role| role.permissions)
            .unwrap_or(Permissions::empty());
        role_ids
            .map(|id| self.cache.role(id))
            .filter_map(|role| role)
            .map(|role| role.permissions)
            .fold(everyone_perms, |acc, perm|  acc | perm)
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
            Ok(self.guild_permissions(guild_id, member.role_ids()))
        } else {
            let roles = self.http_client
                .guild_member(guild_id, user_id)
                .await?
                .into_iter()
                .flat_map(|m| m.roles);
            Ok(self.guild_permissions(guild_id, roles))
        }
    }

    async fn consume_event(self, event: Event) -> () {
        let kind = event.kind();
        let result = match event {
            Event::Ready(_) => Ok(()),
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

        futures::join!(
            self.chunk_guild(guild.id),
            self.refresh_bans(guild.id),
        );

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
        let member = db::Member::fetch(evt.guild_id, self.user_id())
                                .fetch_one(&self.sql)
                                .await?;
        if member.role_ids.contains(&(evt.role_id.0 as i64)) {
            self.refresh_bans(evt.guild_id).await?;
        }

        db::Member::clear_role(evt.guild_id, evt.role_id)
            .execute(&self.sql)
            .await?;
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

        let mut txn = self.sql.begin().await?;
        db::Ban::clear_guild(guild_id).execute(&mut txn).await?;
        if perms.contains(Permissions::ADMINISTRATOR | Permissions::BAN_MEMBERS) {
            debug!("Fetching bans from guild {}", guild_id);
            let bans: Vec<db::Ban> = self.http_client.bans(guild_id).await?
                                         .into_iter().map(|b| db::Ban::from(guild_id, b))
                                         .collect();
            debug!("Fetched {} bans from guild {}", bans.len(), guild_id);
            db::Ban::bulk_insert(bans).execute(&mut txn).await?;
        }
        txn.commit().await?;
        Ok(())
    }

}
