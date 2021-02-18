use crate::hourai::db;
use crate::config::HouraiConfig;
use futures::StreamExt;
use twilight_model::gateway::payload::*;
use twilight_gateway::{Cluster, Intents, Event, EventTypeFlags};
use twilight_gateway::cluster::{ClusterStartError, ShardScheme};
use twilight_cache_inmemory::{InMemoryCache, ResourceType};

// TODO(james7132): Find a way to enable GUILD_PRESENCES without
// blowing up the memory usage.
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_MEMBERS.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::MEMBER_UPDATE.bits() |
        EventTypeFlags::GUILD_DELETE.bits() |
        EventTypeFlags::ROLE_DELETE.bits());

const CACHED_RESOURCES: ResourceType =
    ResourceType::from_bits_truncate(ResourceType::GUILD.bits());

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

    #[inline]
    pub fn http_client(&self) -> &twilight_http::Client {
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

    async fn consume_event(self, event: Event) -> () {
        match event {
            Event::MemberUpdate(evt) => self.on_member_update(*evt).await,
            Event::GuildDelete(evt) => {
                if !evt.unavailable {
                    self.on_guild_leave(*evt).await;
                }
            },
            Event::RoleDelete(evt) => self.on_role_delete(evt).await,
            _ => panic!("Unexpected event type: {:?}", event),
        };
    }

    async fn on_guild_leave(self, evt: GuildDelete) -> () {
        db::MemberRoles::clear_guild(evt.id, &self.sql).await;
    }

    async fn on_role_delete(self, evt: RoleDelete) -> () {
        db::MemberRoles::clear_role(evt.guild_id, evt.role_id, &self.sql).await;
    }

    async fn on_member_update(self, evt: MemberUpdate) -> () {
        if evt.user.bot {
            return;
        }
        db::MemberRoles::new(evt.guild_id, evt.user.id, &evt.roles).log(&self.sql).await;
    }
}
