use crate::hourai::db;
use crate::config::HouraiConfig;
use futures::stream::{StreamExt};
use twilight_model::{
    id::GuildId,
    user::User,
    guild::member::Member,
    gateway::payload::*,
};
use twilight_gateway::{
    Intents, Event, EventTypeFlags,
    shard::raw_message::Message,
    cluster::*,
};
use twilight_cache_inmemory::ResourceType;
use tracing::{info, debug, error};

// TODO(james7132): Find a way to enable GUILD_PRESENCES without
// blowing up the memory usage.
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_BANS.bits() |
    Intents::GUILD_MEMBERS.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::BAN_ADD.bits() |
        EventTypeFlags::BAN_REMOVE.bits() |
        EventTypeFlags::MEMBER_ADD.bits() |
        EventTypeFlags::MEMBER_CHUNK.bits() |
        EventTypeFlags::MEMBER_REMOVE.bits() |
        EventTypeFlags::MEMBER_UPDATE.bits() |
        EventTypeFlags::GUILD_CREATE.bits() |
        EventTypeFlags::GUILD_DELETE.bits() |
        EventTypeFlags::ROLE_DELETE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(ResourceType::GUILD.bits());

#[derive(Clone)]
pub struct Client {
    pub gateway: Cluster,
    pub standby: twilight_standby::Standby,
    //pub cache: InMemoryCache,
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
            //cache: InMemoryCache::builder()
                //.resource_types(CACHED_RESOURCES)
                //.build(),
            standby: twilight_standby::Standby::new(),
            sql: db::create_pg_pool(&config).await.expect("Failed to initialize PostgresSQL")
        })
    }

    fn http_client(&self) -> &twilight_http::Client {
        return &self.gateway.config().http_client();
    }

    pub async fn run(&self) {
        self.gateway.up().await;

        let mut events = self.gateway.some_events(BOT_EVENTS);
        while let Some((_, evt)) = events.next().await {
            self.standby.process(&evt);
            tokio::spawn(self.clone().consume_event(evt));
        }

        self.gateway.down();
    }

    /// Gets the shard ID for a guild.
    pub fn shard_id(&self, guild_id: GuildId) -> u64 {
        let total_shards = self.gateway.config().shard_config().shard()[1];
        assert!(total_shards > 0, "Bot somehow has a total of zero shards.");
        (guild_id.0 >> 22) % total_shards
    }

    pub async fn chunk_guild(&self, guild_id: GuildId) -> Result<(), ClusterSendError> {
        debug!("Chunking guild: {}", guild_id);
        let request = RequestGuildMembers::builder(guild_id).query(String::new(), None);
        let payload = serde_json::to_string(&request)
            .expect("Could not serialize valid input");
        self.gateway.send(self.shard_id(guild_id), Message::Text(payload)).await
    }

    async fn consume_event(self, event: Event) -> () {
        match event {
            Event::BanAdd(evt) => {self.on_ban_add(evt).await;},
            Event::BanRemove(evt) => {self.on_ban_remove(evt).await;},
            Event::GuildCreate(evt) => {
                if !evt.0.unavailable {
                    self.on_guild_join(*evt).await;
                }
            },
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.on_guild_leave(*evt).await;
                }
            },
            Event::MemberAdd(evt) => {self.on_member_add(*evt).await;},
            Event::MemberChunk(evt) => {self.on_member_chunk(evt).await;},
            Event::MemberRemove(evt) => {self.on_member_remove(evt).await;},
            Event::MemberUpdate(evt) => {self.on_member_update(*evt).await;},
            Event::RoleDelete(evt) => self.on_role_delete(evt).await,
            _ => error!("Unexpected event type: {:?}", event),
        };
    }

    async fn on_ban_add(self, evt: BanAdd) -> () {
        // TODO(james7132): Log ban here
        let user_id = evt.user.id;
        if let Err(err) = self.log_users(vec![evt.user]).await {
            error!("Error while logging user {}: {:?}", user_id, err);
        }
    }

    async fn on_ban_remove(self, evt: BanRemove) -> () {
        // TODO(james7132): Log ban here
        let user_id = evt.user.id;
        if let Err(err) = self.log_users(vec![evt.user]).await {
            error!("Error while logging user {}: {:?}", user_id, err);
        }
    }

    async fn on_member_add(self, evt: MemberAdd) -> () {
        let guild_id = evt.guild_id;
        let user_id = evt.0.user.id;
        if let Err(err) = self.log_members(vec![evt.0]).await {
            error!("Error while logging member of guild {}: {:?}", guild_id, err);
        }
    }

    async fn on_member_chunk(self, evt: MemberChunk) -> () {
        if let Err(err) = self.log_members(evt.members).await {
            error!("Error while logging members of guild {}: {:?}", evt.guild_id, err);
        }
    }

    async fn on_member_remove(self, evt: MemberRemove) -> () {
        let user_id = evt.user.id;
        if let Err(err) = self.log_users(vec![evt.user]).await {
            error!("Error while logging user {}: {:?}", user_id, err);
        }
    }

    async fn on_guild_available(self, evt: GuildCreate) {
        info!("Guild Available: {}", evt.0.id);
        self.chunk_guild(evt.0.id).await;
    }

    async fn on_guild_join(self, evt: GuildCreate) -> () {
        info!("Joined Guild: {}", evt.0.id);
        self.chunk_guild(evt.0.id).await;
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> () {
        db::MemberRoles::clear_guild(evt.id, &self.sql).await;
    }

    async fn on_role_delete(self, evt: RoleDelete) -> () {
        db::MemberRoles::clear_role(evt.guild_id, evt.role_id, &self.sql).await;
    }

    async fn on_member_update(self, evt: MemberUpdate) -> Result<(), sqlx::Error> {
        if evt.user.bot {
            return Ok(());
        }

        let mut txn = self.sql.begin().await?;
        db::MemberRoles::new(evt.guild_id, evt.user.id, &evt.roles)
            .insert()
            .execute(&mut txn)
            .await?;
        db::Username::new(&evt.user)
            .insert()
            .execute(&mut txn)
            .await?;
        if let Err(err) = txn.commit().await {
            error!("Failed to log member data {}: {:?}", evt.user.id, err);
            Err(err)
        } else {
            Ok(())
        }
    }

    async fn log_users(&self, users: Vec<User>) -> Result<(), sqlx::Error> {
        let mut txn = self.sql.begin().await?;
        for user in users {
            if !user.bot {
                db::Username::new(&user)
                    .insert()
                    .execute(&mut txn)
                    .await?;
            }
        }
        txn.commit().await?;
        Ok(())
    }

    async fn log_members(&self, members: Vec<Member>) -> Result<(), sqlx::Error> {
        let mut txn = self.sql.begin().await?;
        for member in members {
            if !member.user.bot {
                db::MemberRoles::new(member.guild_id, member.user.id, &member.roles)
                    .insert()
                    .execute(&mut txn)
                    .await?;
                db::Username::new(&member.user)
                    .insert()
                    .execute(&mut txn)
                    .await?;
            }
        }
        txn.commit().await?;
        Ok(())
    }

}
