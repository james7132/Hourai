use crate::hourai::db;
use crate::error::Result;
use crate::config::HouraiConfig;
use futures::stream::StreamExt;
use twilight_model::{
    id::*,
    user::User,
    guild::Permissions,
    guild::member::Member,
    gateway::payload::*,
};
use twilight_gateway::{
    Intents, Event, EventTypeFlags,
    shard::raw_message::Message,
    cluster::*,
};
use twilight_cache_inmemory::{InMemoryCache, ResourceType};
use tracing::{info, debug, error};

// TODO(james7132): Find a way to enable GUILD_PRESENCES without
// blowing up the memory usage.
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::DIRECT_MESSAGES.bits() |
    Intents::GUILDS.bits() |
    Intents::GUILD_BANS.bits() |
    Intents::GUILD_MESSAGES.bits() |
    Intents::GUILD_MEMBERS.bits());

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
        EventTypeFlags::GUILD_DELETE.bits() |
        EventTypeFlags::ROLE_CREATE.bits() |
        EventTypeFlags::ROLE_UPDATE.bits() |
        EventTypeFlags::ROLE_DELETE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(
        ResourceType::ROLE.bits() |
        ResourceType::GUILD.bits() |
        ResourceType::USER_CURRENT.bits());

#[derive(Clone)]
pub struct Client {
    pub gateway: Cluster,
    pub standby: twilight_standby::Standby,
    pub cache: InMemoryCache,
    pub sql: sqlx::PgPool,
}

impl Client {

    pub async fn new(config: &HouraiConfig)
        -> std::result::Result<Self, ClusterStartError> {
        Ok(Self {
            gateway: Cluster::builder(&config.discord.bot_token, BOT_INTENTS)
                .shard_scheme(ShardScheme::Auto)
                .build()
                .await?,
            cache: InMemoryCache::builder()
                .resource_types(CACHED_RESOURCES)
                .build(),
            standby: twilight_standby::Standby::new(),
            sql: db::create_pg_pool(&config).await.expect("Failed to initialize PostgresSQL")
        })
    }

    fn http_client(&self) -> &twilight_http::Client {
        return &self.gateway.config().http_client();
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
            tokio::spawn(self.clone().consume_event(evt));
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
        let request = RequestGuildMembers::builder(guild_id).query(String::new(), None);
        let payload = serde_json::to_string(&request)
            .expect("Could not serialize valid input");
        self.gateway.send(self.shard_id(guild_id), Message::Text(payload)).await?;
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
        let member = db::Member::fetch(guild_id, user_id)
                                .fetch_one(&self.sql)
                                .await?;
        Ok(self.guild_permissions(guild_id, member.role_ids()))
    }

    async fn consume_event(self, event: Event) -> () {
        let kind = event.kind();
        let result = match event.clone() {
            Event::Ready(_) => Ok(()),
            Event::BanAdd(evt) => self.on_ban_add(evt).await,
            Event::BanRemove(evt) => self.on_ban_remove(evt).await,
            Event::GuildCreate(evt) => self.on_guild_create(*evt).await,
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
            if let Some(ban) = self.http_client().ban(evt.guild_id, evt.user.id).await? {
                db::Ban::from(evt.guild_id, ban)
                    .insert()
                    .execute(&self.sql)
                    .await?;
            }
        }

        Ok(())
    }

    async fn on_ban_remove(self, evt: BanRemove) -> Result<()> {
        self.log_users(vec![evt.user.clone()]).await?;
        db::Ban::clear_ban(evt.guild_id, evt.user.id).execute(&self.sql).await?;
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
        self.log_members(&guild.members).await?;
        self.refresh_bans(guild.id).await?;

        Ok(())
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> Result<()> {
        db::Member::clear_guild(evt.id).execute(&self.sql).await?;
        db::Ban::clear_guild(evt.id).execute(&self.sql).await?;
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

        let mut txn = self.sql.begin().await?;
        db::Member {
            guild_id: evt.guild_id.0 as i64,
            user_id: evt.user.id.0 as i64,
            role_ids: evt.roles.iter().map(|id| id.0 as i64).collect(),
            nickname: evt.nick
        }.insert()
         .execute(&mut txn)
         .await?;
        db::Username::new(&evt.user)
            .insert()
            .execute(&mut txn)
            .await?;
        txn.commit().await?;
        Ok(())
    }

    async fn log_users(&self, users: Vec<User>) -> Result<()> {
        let usernames = users.iter().map(|u| db::Username::new(u)).collect();
        db::Username::bulk_insert(usernames).execute(&self.sql).await?;
        Ok(())
    }

    async fn log_members(&self, members: &Vec<Member>) -> Result<()> {
        let usernames = members.iter().map(|m| db::Username::new(&m.user)).collect();

        let mut txn = self.sql.begin().await?;
        db::Username::bulk_insert(usernames).execute(&mut txn).await?;
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
            let bans: Vec<db::Ban> = self.http_client().bans(guild_id).await?
                                         .into_iter().map(|b| db::Ban::from(guild_id, b))
                                         .collect();
            debug!("Fetched {} bans from guild {}", bans.len(), guild_id);
            db::Ban::bulk_insert(bans).execute(&mut txn).await?;
        }
        txn.commit().await?;
        Ok(())
    }

}
