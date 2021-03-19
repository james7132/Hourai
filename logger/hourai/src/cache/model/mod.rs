//! Models built for utilizing efficient caching.

mod channel;
mod guild;
mod member;
mod message;

pub use self::{
    guild::CachedGuild, member::CachedMember, message::CachedMessage, channel::CachedChannel
};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedGuild, CachedMember, CachedVoiceState, CachedChannel};
    }
}
