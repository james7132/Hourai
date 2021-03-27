//! Models built for utilizing efficient caching.

mod channel;
mod role;
mod guild;

pub use self::{
    guild::CachedGuild, channel::CachedChannel, role::CachedRole
};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedGuild, CachedChannel, CachedRole};
    }
}
