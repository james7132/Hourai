use super::{
    config::{Config, ResourceType},
    InMemoryCache,
};

/// Builder to configure and construct an [`InMemoryCache`].
#[derive(Clone, Debug, Default, Eq, PartialEq)]
pub struct InMemoryCacheBuilder(Config);

impl InMemoryCacheBuilder {
    /// Creates a builder to configure and construct an [`InMemoryCache`].
    pub fn new() -> Self {
        Self::default()
    }

    /// Consume the builder, returning a configured cache.
    pub fn build(self) -> InMemoryCache {
        InMemoryCache::new_with_config(self.0)
    }

    /// Sets the list of resource types for the cache to handle.
    ///
    /// Defaults to all types.
    pub fn resource_types(mut self, resource_types: ResourceType) -> Self {
        self.0.resource_types = resource_types;

        self
    }
}

#[cfg(test)]
mod tests {
    use super::InMemoryCacheBuilder;
    use static_assertions::assert_impl_all;
    use std::fmt::Debug;

    assert_impl_all!(InMemoryCacheBuilder: Clone, Debug, Default, Send, Sync);
}
