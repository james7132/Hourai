//! Models built for utilizing efficient caching.

mod guild;
mod role;

pub use self::{guild::CachedGuild, role::CachedRole};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedGuild, CachedRole};
    }
}
