//! Models built for utilizing efficient caching.

mod channel;
mod guild;
mod role;

pub use self::{channel::CachedChannel, guild::CachedGuild, role::CachedRole};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedChannel, CachedGuild, CachedRole};
    }
}
