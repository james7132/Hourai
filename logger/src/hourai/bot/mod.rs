use crate::hourai::db;
use crate::config::HouraiConfig;
use rand::Rng;
use futures::stream::{self, Stream, StreamExt};
use twilight_model::{
    id::GuildId,
    guild::member::Member,
    gateway::payload::*
};
use twilight_gateway::{
    Intents, Event, EventTypeFlags,
    shard::raw_message::Message,
    cluster::*,
};
use twilight_cache_inmemory::{InMemoryCache, ResourceType};
use tracing::error;

// TODO(james7132): Find a way to enable GUILD_PRESENCES without
// blowing up the memory usage.
const BOT_INTENTS: Intents = Intents::from_bits_truncate(
    Intents::GUILDS.bits() |
    Intents::GUILD_MEMBERS.bits());

const BOT_EVENTS : EventTypeFlags =
    EventTypeFlags::from_bits_truncate(
        EventTypeFlags::MEMBER_CHUNK.bits() |
        EventTypeFlags::MEMBER_UPDATE.bits() |
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

    /// Fetc&hes a stream of Member objects
    pub async fn fetch_guild_members(&self, request: &RequestGuildMembers) ->
        Result<impl Stream<Item=Member>, ClusterSendError> {

        // Generate random nonce
        let nonce: String = {
            let mut rng = rand::thread_rng();
            std::iter::repeat(())
                .map(|()| rng.sample(rand::distributions::Alphanumeric))
                .map(char::from)
                .take(32)
                .collect()
        };

        let payload = serde_json::to_string(&request).expect("Could not serialize valid input");
        self.gateway.send(self.shard_id(request.d.guild_id), Message::Text(payload)).await?;
        let stream = self.standby.wait_for_stream(request.d.guild_id, move |evt: &Event| {
                match evt {
                    Event::MemberChunk(MemberChunk{nonce: Some(n), ..}) => n == &nonce,
                    _ => false
                }
            })
            .map(|evt| {
                match evt {
                    Event::MemberChunk(chunk) => chunk,
                    _ => panic!("Only MemberChunk events should be allowed")
                }
            })
            .take_while(|evt| futures::future::ready(evt.chunk_index <= evt.chunk_count))
            .flat_map(|evt| stream::iter(evt.members));

        Ok(stream)
    }

    async fn consume_event(self, event: Event) -> () {
        match event {
            Event::GuildCreate(evt) => {
                if !evt.0.unavailable {
                    self.on_guild_join(*evt).await
                }
            },
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

    async fn on_guild_join(self, evt: GuildCreate) -> () {
        let guild_id = evt.0.id;
        let request = RequestGuildMembers::builder(guild_id).query(String::new(), None);
        match self.fetch_guild_members(&request).await {
            Err(err) => error!("Error while chunking members for guild {}: {:?}", guild_id, err),
            Ok(members) => {
                members.chunks(1000)
                    .for_each_concurrent(None, |chunk| async {
                        if let Err(err) = self.log_members(chunk).await {
                            error!("Error while logging new guild {}: {:?}", guild_id, err);
                        }
                    })
                    .await;
            }
        };
    }

    async fn log_members(&self, members: Vec<Member>) -> Result<(), sqlx::Error> {
        let mut txn = self.sql.begin().await?;
        for member in members.iter() {
            if !member.user.bot {
                db::MemberRoles::new(member.guild_id, member.user.id, &member.roles)
                    .insert()
                    .execute(&mut txn)
                    .await;
                db::Username::new(&member.user)
                    .insert()
                    .execute(&mut txn)
                    .await;
            }
        }
        txn.commit().await?;
        Ok(())
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
        db::Username::new(&evt.user).log(&self.sql).await;
    }
}
