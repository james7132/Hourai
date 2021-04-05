use bitflags::bitflags;

bitflags! {
    /// A set of bitflags which can be used to specify what resource to process
    /// into the cache.
    pub struct ResourceType: u8 {
        const GUILD = 1;
        const MEMBER = 1 << 1;
        const PRESENCE = 1 << 2;
        const VOICE_STATE = 1 << 3;
    }
}

/// Configuration for an [`InMemoryCache`].
///
/// [`InMemoryCache`]: crate::InMemoryCache
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct Config {
    pub(super) resource_types: ResourceType,
}

impl Config {
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
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{Config, ResourceType};

    #[test]
    #[allow(clippy::cognitive_complexity)]
    fn test_resource_type_const_values() {
        assert_eq!(1, ResourceType::GUILD.bits());
        assert_eq!(1 << 1, ResourceType::MEMBER.bits());
        assert_eq!(1 << 2, ResourceType::PRESENCE.bits());
        assert_eq!(1 << 3, ResourceType::VOICE_STATE.bits());
    }

    #[test]
    fn test_defaults() {
        let conf = Config {
            resource_types: ResourceType::all(),
        };
        let default = Config::default();
        assert_eq!(conf.resource_types, default.resource_types);
    }

    #[test]
    fn test_config_fields() {
        static_assertions::assert_fields!(Config: resource_types);
    }
}
