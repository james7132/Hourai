//! Models built for utilizing efficient caching.

mod emoji;
mod guild;
mod member;
mod message;

pub use self::{
    emoji::CachedEmoji, guild::CachedGuild, member::CachedMember, message::CachedMessage,
};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedEmoji, CachedGuild, CachedMember, CachedVoiceState};
    }
}
