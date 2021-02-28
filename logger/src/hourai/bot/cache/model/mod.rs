//! Models built for utilizing efficient caching.

mod emoji;
mod guild;
mod member;
mod message;
mod voice_state;

pub use self::{
    emoji::CachedEmoji, guild::CachedGuild, member::CachedMember, message::CachedMessage,
    voice_state::CachedVoiceState,
};

#[cfg(tests)]
mod tests {
    #[test]
    fn test_reexports() {
        use super::{CachedEmoji, CachedGuild, CachedMember, CachedVoiceState};
    }
}
