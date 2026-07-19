use super::{InMemoryCache, config::ResourceType};
use std::ops::Deref;
use twilight_model::gateway::{event::Event, payload::incoming::*, presence::UserOrId};

pub trait UpdateCache {
    // Allow this for presentation purposes in documentation.
    fn update(&self, _cache: &InMemoryCache) {}
}

impl UpdateCache for Event {
    fn update(&self, c: &InMemoryCache) {
        match self {
            Self::GuildCreate(v) => c.update(v.deref()),
            Self::GuildDelete(v) => c.update(v),
            Self::MemberAdd(v) => c.update(v.deref()),
            Self::MemberRemove(v) => c.update(v),
            Self::MemberUpdate(v) => c.update(v.deref()),
            Self::MemberChunk(v) => c.update(v),
            Self::PresenceUpdate(v) => c.update(v.deref()),
            Self::Ready(v) => c.update(v.deref()),
            Self::UnavailableGuild(v) => c.update(v),
            _ => {}
        }
    }
}

impl UpdateCache for GuildCreate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::GUILD) {
            return;
        }

        cache.cache_guild_create(self);
    }
}

impl UpdateCache for GuildDelete {
    fn update(&self, cache: &InMemoryCache) {
        let id = self.id;

        if cache.wants(ResourceType::GUILD) {
            cache.0.guilds.remove(&id);
        }

        if cache.wants(ResourceType::MEMBER) {
            cache.0.pending_members.retain(|kv| kv.0 != id);
        }

        if cache.wants(ResourceType::PRESENCE) {
            cache.0.guild_presences.remove(&id);
        }
    }
}

impl UpdateCache for MemberAdd {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::MEMBER) {
            return;
        }

        cache.cache_member(self.guild_id, &self.member);
    }
}

impl UpdateCache for MemberChunk {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::MEMBER) {
            return;
        }

        if self.members.is_empty() {
            return;
        }

        cache.cache_members(self.guild_id, &self.members);
    }
}

impl UpdateCache for MemberRemove {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::MEMBER) {
            return;
        }

        cache
            .0
            .pending_members
            .remove(&(self.guild_id, self.user.id));
    }
}

impl UpdateCache for MemberUpdate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::MEMBER) {
            return;
        }

        let id = (self.guild_id, self.user.id);
        if self.pending {
            cache.0.pending_members.insert(id);
        } else {
            cache.0.pending_members.remove(&id);
        }
    }
}

impl UpdateCache for PresenceUpdate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::PRESENCE) {
            return;
        }

        let user_id = match self.user {
            UserOrId::User(ref u) => u.id,
            UserOrId::UserId { id } => id,
        };

        cache.cache_presence(self.guild_id, user_id, self.status);
    }
}

impl UpdateCache for Ready {
    fn update(&self, cache: &InMemoryCache) {
        if cache.wants(ResourceType::GUILD) {
            for guild in &self.guilds {
                cache.unavailable_guild(guild.id);
            }
        }
    }
}

impl UpdateCache for UnavailableGuild {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::GUILD) {
            return;
        }

        cache.0.guilds.remove(&self.id);
        cache.0.unavailable_guilds.insert(self.id);
    }
}
