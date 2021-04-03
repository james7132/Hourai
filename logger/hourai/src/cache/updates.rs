use super::{config::ResourceType, InMemoryCache};
use dashmap::DashMap;
use std::{collections::HashSet, hash::Hash, ops::Deref, sync::Arc};
use twilight_model::{
    gateway::{event::Event, payload::*, presence::UserOrId},
    guild::GuildStatus,
    id::GuildId,
};

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
            GuildUpdate(v) => c.update(v.deref()),
            MemberAdd(v) => c.update(v.deref()),
            MemberRemove(v) => c.update(v),
            MemberUpdate(v) => c.update(v.deref()),
            MemberChunk(v) => c.update(v),
            PresenceUpdate(v) => c.update(v.deref()),
            Ready(v) => c.update(v.deref()),
            RoleCreate(v) => c.update(v),
            RoleDelete(v) => c.update(v),
            RoleUpdate(v) => c.update(v),
            UnavailableGuild(v) => c.update(v),
            VoiceServerUpdate(v) => c.update(v),
            VoiceStateUpdate(v) => c.update(v.deref()),
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
        fn remove_ids<T: Eq + Hash, U>(
            guild_map: &DashMap<GuildId, HashSet<T>>,
            container: &DashMap<T, U>,
            guild_id: GuildId,
        ) {
            if let Some((_, ids)) = guild_map.remove(&guild_id) {
                for id in ids {
                    container.remove(&id);
                }
            }
        }

        if !cache.wants(ResourceType::GUILD) {
            return;
        }

        let id = self.id;

        cache.0.guilds.remove(&id);

        if cache.wants(ResourceType::ROLE) {
            remove_ids(&cache.0.guild_roles, &cache.0.roles, id);
        }

        if cache.wants(ResourceType::VOICE_STATE) {
            cache.0.voice_states.retain(|(g, _), _| *g != id);
        }

        if cache.wants(ResourceType::MEMBER) {
            cache.0.pending_members.retain(|kv| kv.0 != id);
        }

        if cache.wants(ResourceType::PRESENCE) {
            cache.0.guild_presences.remove(&id);
        }
    }
}

impl UpdateCache for GuildUpdate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::GUILD) {
            return;
        }

        if let Some(mut guild) = cache.0.guilds.get_mut(&self.0.id) {
            let mut guild = Arc::make_mut(&mut guild);
            guild.name = self.name.clone().into_boxed_str();
            guild.description = self.description.clone().map(|s| s.into_boxed_str());
            guild.features = self
                .features
                .iter()
                .map(|s| s.clone().into_boxed_str())
                .collect();
            guild.icon = self.icon.clone().map(|s| s.into_boxed_str());
            guild.owner_id = self.owner_id;
            guild.premium_tier = self.premium_tier;
            guild
                .premium_subscription_count
                .replace(self.premium_subscription_count.unwrap_or_default());
            guild.vanity_url_code = self.vanity_url_code.clone().map(|s| s.into_boxed_str());
        };
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
            for status in &self.guilds {
                match status {
                    GuildStatus::Offline(u) => {
                        cache.unavailable_guild(u.id);
                    }
                    GuildStatus::Online(g) => {
                        cache.cache_guild(g.clone());
                    }
                }
            }
        }
    }
}

impl UpdateCache for RoleCreate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::ROLE) {
            return;
        }

        cache.cache_role(self.guild_id, &self.role);
    }
}

impl UpdateCache for RoleDelete {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::ROLE) {
            return;
        }

        cache.delete_role(self.role_id);
    }
}

impl UpdateCache for RoleUpdate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::ROLE) {
            return;
        }

        cache.cache_role(self.guild_id, &self.role);
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

impl UpdateCache for VoiceServerUpdate {
    fn update(&self, _: &InMemoryCache) {}
}

impl UpdateCache for VoiceStateUpdate {
    fn update(&self, cache: &InMemoryCache) {
        if !cache.wants(ResourceType::VOICE_STATE) {
            return;
        }

        if let Some(guild_id) = &self.0.guild_id {
            cache.cache_voice_state(*guild_id, self.0.user_id, self.0.channel_id);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::super::config::ResourceType;
    use super::*;
    use twilight_model::{
        channel::{
            message::{MessageFlags, MessageType},
            ChannelType, GuildChannel, Message, Reaction, TextChannel,
        },
        gateway::payload::{reaction_remove_emoji::PartialEmoji, ChannelDelete},
        guild::{
            DefaultMessageNotificationLevel, ExplicitContentFilter, Guild, Member, MfaLevel,
            PartialGuild, PartialMember, PremiumTier, SystemChannelFlags, VerificationLevel,
        },
        id::{ChannelId, GuildId, MessageId, UserId},
        user::User,
        voice::VoiceState,
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

    #[test]
    fn test_guild_update() {
        let cache = InMemoryCache::new();
        let guild = Guild {
            afk_channel_id: None,
            afk_timeout: 0,
            application_id: None,
            approximate_member_count: None,
            approximate_presence_count: None,
            banner: None,
            channels: Vec::new(),
            default_message_notifications: DefaultMessageNotificationLevel::Mentions,
            description: None,
            discovery_splash: None,
            emojis: Vec::new(),
            explicit_content_filter: ExplicitContentFilter::None,
            features: Vec::new(),
            icon: None,
            id: GuildId(1),
            joined_at: None,
            large: false,
            lazy: None,
            max_members: None,
            max_presences: None,
            max_video_channel_users: None,
            member_count: None,
            members: Vec::new(),
            mfa_level: MfaLevel::None,
            name: "test".to_owned(),
            owner_id: UserId(1),
            owner: None,
            permissions: None,
            preferred_locale: "en_us".to_owned(),
            premium_subscription_count: None,
            premium_tier: PremiumTier::None,
            presences: Vec::new(),
            region: "us".to_owned(),
            roles: Vec::new(),
            rules_channel_id: None,
            splash: None,
            system_channel_flags: SystemChannelFlags::empty(),
            system_channel_id: None,
            unavailable: false,
            vanity_url_code: None,
            verification_level: VerificationLevel::VeryHigh,
            voice_states: Vec::new(),
            widget_channel_id: None,
            widget_enabled: None,
        };

        cache.update(&GuildCreate(guild.clone()));

        let mutation = PartialGuild {
            id: guild.id,
            afk_channel_id: guild.afk_channel_id,
            afk_timeout: guild.afk_timeout,
            application_id: guild.application_id,
            banner: guild.banner,
            default_message_notifications: guild.default_message_notifications,
            description: guild.description,
            discovery_splash: guild.discovery_splash,
            emojis: guild.emojis,
            explicit_content_filter: guild.explicit_content_filter,
            features: guild.features,
            icon: guild.icon,
            max_members: guild.max_members,
            max_presences: guild.max_presences,
            member_count: guild.member_count,
            mfa_level: guild.mfa_level,
            name: "test2222".to_owned(),
            owner_id: UserId(2),
            owner: guild.owner,
            permissions: guild.permissions,
            preferred_locale: guild.preferred_locale,
            premium_subscription_count: guild.premium_subscription_count,
            premium_tier: guild.premium_tier,
            region: guild.region,
            roles: guild.roles,
            rules_channel_id: guild.rules_channel_id,
            splash: guild.splash,
            system_channel_flags: guild.system_channel_flags,
            system_channel_id: guild.system_channel_id,
            verification_level: guild.verification_level,
            vanity_url_code: guild.vanity_url_code,
            widget_channel_id: guild.widget_channel_id,
            widget_enabled: guild.widget_enabled,
        };

        cache.update(&GuildUpdate(mutation.clone()));

        assert_eq!(cache.guild(guild.id).unwrap().name, mutation.name);
        assert_eq!(cache.guild(guild.id).unwrap().owner_id, mutation.owner_id);
        assert_eq!(cache.guild(guild.id).unwrap().id, mutation.id);
    }

    #[test]
    fn test_voice_states_with_no_cached_guilds() {
        let cache = InMemoryCache::builder()
            .resource_types(ResourceType::VOICE_STATE)
            .build();

        cache.update(&VoiceStateUpdate(VoiceState {
            channel_id: None,
            deaf: false,
            guild_id: Some(GuildId(1)),
            member: None,
            mute: false,
            self_deaf: false,
            self_mute: false,
            self_stream: false,
            session_id: "38fj3jfkh3pfho3prh2".to_string(),
            suppress: false,
            token: None,
            user_id: UserId(1),
        }));
    }
}
