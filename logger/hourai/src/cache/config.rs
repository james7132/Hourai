use bitflags::bitflags;

bitflags! {
    /// A set of bitflags which can be used to specify what resource to process
    /// into the cache.
    pub struct ResourceType: u8 {
        const CHANNEL = 1;
        const GUILD = 1 << 2;
        const MEMBER = 1 << 3;
        const PRESENCE = 1 << 4;
        const ROLE = 1 << 5;
        const USER_CURRENT = 1 << 6;
        const VOICE_STATE = 1 << 7;
    }
}

/// Configuration for an [`InMemoryCache`].
///
/// [`InMemoryCache`]: crate::InMemoryCache
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Config {
    pub(super) resource_types: ResourceType,
    pub(super) message_cache_size: usize,
}

impl Config {
    /// Returns an immutable reference to the message cache size.
    pub fn message_cache_size(&self) -> usize {
        self.message_cache_size
    }

    /// Returns a mutable reference to the message cache size.
    pub fn message_cache_size_mut(&mut self) -> &mut usize {
        &mut self.message_cache_size
    }
    /// Returns an immutable reference to the resource types enabled.
    pub fn resource_types(&self) -> ResourceType {
        self.resource_types
    }

    /// Returns a mutable reference to the resource types enabled.
    pub fn resource_types_mut(&mut self) -> &mut ResourceType {
        &mut self.resource_types
    }
}

impl Default for Config {
    fn default() -> Self {
        Self {
            resource_types: ResourceType::all(),
            message_cache_size: 100,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{Config, ResourceType};

    #[test]
    #[allow(clippy::cognitive_complexity)]
    fn test_resource_type_const_values() {
        assert_eq!(1, ResourceType::CHANNEL.bits());
        assert_eq!(1 << 2, ResourceType::GUILD.bits());
        assert_eq!(1 << 3, ResourceType::MEMBER.bits());
        assert_eq!(1 << 4, ResourceType::PRESENCE.bits());
        assert_eq!(1 << 5, ResourceType::ROLE.bits());
        assert_eq!(1 << 6, ResourceType::USER_CURRENT.bits());
        assert_eq!(1 << 7, ResourceType::VOICE_STATE.bits());
    }

    #[test]
    fn test_defaults() {
        let conf = Config {
            resource_types: ResourceType::all(),
            message_cache_size: 100,
        };
        let default = Config::default();
        assert_eq!(conf.resource_types, default.resource_types);
        assert_eq!(conf.message_cache_size, default.message_cache_size);
    }

    #[test]
    fn test_config_fields() {
        static_assertions::assert_fields!(Config: resource_types, message_cache_size);
    }
}
