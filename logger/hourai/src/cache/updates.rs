use super::{config::ResourceType, InMemoryCache};
use std::ops::Deref;
use twilight_model::gateway::{event::Event, payload::*, presence::UserOrId};

pub trait UpdateCache {
    // Allow this for presentation purposes in documentation.
    #[allow(unused_variables)]
    fn update(&self, cache: &InMemoryCache) {}
}

impl UpdateCache for Event {
    #[allow(clippy::cognitive_complexity)]
    fn update(&self, c: &InMemoryCache) {
        use Event::*;

        match self {
            GuildCreate(v) => c.update(v.deref()),
            GuildDelete(v) => c.update(v.deref()),
            MemberAdd(v) => c.update(v.deref()),
            MemberRemove(v) => c.update(v),
            MemberUpdate(v) => c.update(v.deref()),
            MemberChunk(v) => c.update(v),
            PresenceUpdate(v) => c.update(v.deref()),
            Ready(v) => c.update(v.deref()),
            UnavailableGuild(v) => c.update(v),
            _ => {}
        }
    }
}

impl UpdateCache for GuildCreate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::GUILD) {
            return;
        }

        cache.cache_guild(self.0.clone());
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

        cache.cache_member(self.guild_id, &self.0);
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

        cache.cache_members(self.guild_id, self.members.clone());
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

#[cfg(test)]
mod tests {
    use twilight_model::{
        channel::{
            ChannelType, GuildChannel, TextChannel,
        },
        id::{ChannelId, GuildId},
    };

    fn guild_channel_text() -> (GuildId, ChannelId, GuildChannel) {
        let guild_id = GuildId(1);
        let channel_id = ChannelId(2);
        let channel = GuildChannel::Text(TextChannel {
            guild_id: Some(guild_id),
            id: channel_id,
            kind: ChannelType::GuildText,
            last_message_id: None,
            last_pin_timestamp: None,
            name: "test".to_owned(),
            nsfw: false,
            parent_id: None,
            permission_overwrites: Vec::new(),
            position: 3,
            rate_limit_per_user: None,
            topic: None,
        });

        (guild_id, channel_id, channel)
    }
}
