mod builder;
mod config;
mod updates;

pub use self::{
    builder::InMemoryCacheBuilder,
    config::{Config, ResourceType},
    updates::UpdateCache,
};

use dashmap::{DashMap, DashSet};
use std::{collections::HashSet, sync::Arc};
use twilight_model::{
    gateway::presence::{Presence, Status, UserOrId},
    guild::{Guild, Member},
    id::{
        marker::{GuildMarker, UserMarker},
        Id,
    },
};

// When adding a field here, be sure to add it to `InMemoryCache::clear` if
// necessary.
#[derive(Debug, Default)]
struct InMemoryCacheRef {
    config: Arc<Config>,
    guilds: DashSet<Id<GuildMarker>>,
    guild_presences: DashMap<Id<GuildMarker>, HashSet<Id<UserMarker>>>,
    unavailable_guilds: DashSet<Id<GuildMarker>>,
    pending_members: DashSet<(Id<GuildMarker>, Id<UserMarker>)>,
}

#[derive(Clone, Debug, Default)]
pub struct InMemoryCache(Arc<InMemoryCacheRef>);

impl InMemoryCache {
    pub fn new() -> Self {
        Self::default()
    }

    fn new_with_config(config: Config) -> Self {
        Self(Arc::new(InMemoryCacheRef {
            config: Arc::new(config),
            ..Default::default()
        }))
    }

    pub fn builder() -> InMemoryCacheBuilder {
        InMemoryCacheBuilder::new()
    }

    pub fn config(&self) -> Config {
        (*self.0.config).clone()
    }

    pub fn update(&self, value: &impl UpdateCache) {
        value.update(self);
    }

    pub fn is_pending(&self, guild_id: Id<GuildMarker>, user_id: Id<UserMarker>) -> bool {
        self.0.pending_members.contains(&(guild_id, user_id))
    }

    pub fn guilds(&self) -> Vec<Id<GuildMarker>> {
        self.0.guilds.iter().map(|r| *r.key()).collect()
    }

    pub fn guild_online(&self, guild_id: Id<GuildMarker>) -> Option<HashSet<Id<UserMarker>>> {
        self.0
            .guild_presences
            .get(&guild_id)
            .map(|r| r.value().clone())
    }

    pub fn presence(&self, guild_id: Id<GuildMarker>, user_id: Id<UserMarker>) -> bool {
        self.0
            .guild_presences
            .get(&guild_id)
            .map(|p| p.contains(&user_id))
            .unwrap_or(false)
    }

    pub fn clear(&self) {
        self.0.guilds.clear();
        self.0.guild_presences.clear();
        self.0.unavailable_guilds.clear();
        self.0.pending_members.clear();
    }

    pub fn cache_guild_create(
        &self,
        guild: &twilight_model::gateway::payload::incoming::GuildCreate,
    ) {
        match guild {
            twilight_model::gateway::payload::incoming::GuildCreate::Available(g) => {
                if self.wants(ResourceType::MEMBER) {
                    self.cache_members(g.id, g.members.clone());
                }
                if self.wants(ResourceType::PRESENCE) {
                    self.0.guild_presences.insert(g.id, HashSet::new());
                    self.cache_presences(g.id, g.presences.clone());
                }
                self.0.guilds.insert(g.id);
                self.0.unavailable_guilds.remove(&g.id);
            }
            twilight_model::gateway::payload::incoming::GuildCreate::Unavailable(g) => {
                self.unavailable_guild(g.id);
            }
        }
    }

    fn cache_member(&self, guild_id: Id<GuildMarker>, member: &Member) {
        let id = (guild_id, member.user.id);
        if member.pending {
            self.0.pending_members.insert(id);
        } else {
            self.0.pending_members.remove(&id);
        }
    }

    fn cache_members(&self, guild_id: Id<GuildMarker>, members: impl IntoIterator<Item = Member>) {
        if self.wants(ResourceType::MEMBER) {
            for member in members {
                self.cache_member(guild_id, &member);
            }
        }
    }

    fn cache_presences(
        &self,
        guild_id: Id<GuildMarker>,
        presences: impl IntoIterator<Item = Presence>,
    ) {
        if self.wants(ResourceType::PRESENCE) {
            for presence in presences {
                let user_id = match presence.user {
                    UserOrId::User(ref u) => u.id,
                    UserOrId::UserId { id } => id,
                };
                self.cache_presence(guild_id, user_id, presence.status);
            }
        }
    }

    fn cache_presence(&self, guild_id: Id<GuildMarker>, user_id: Id<UserMarker>, status: Status) {
        if self.wants(ResourceType::PRESENCE) && status != Status::Offline {
            if let Some(mut set) = self.0.guild_presences.get_mut(&guild_id) {
                set.insert(user_id);
            }
        }
    }

    fn unavailable_guild(&self, guild_id: Id<GuildMarker>) {
        if self.wants(ResourceType::GUILD) {
            self.0.guilds.remove(&guild_id);
            self.0.unavailable_guilds.insert(guild_id);
        }
    }

    fn wants(&self, resource_type: ResourceType) -> bool {
        self.0.config.resource_types().contains(resource_type)
    }
}
