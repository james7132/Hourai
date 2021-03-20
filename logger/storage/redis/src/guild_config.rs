use hourai::proto::{auto_config::*, guild_configs::*};

pub trait CachedGuildConfig {
    const SUBKEY: u8;
}

macro_rules! guild_config {
    ($proto: ty, $key: expr) => {
        impl CachedGuildConfig for $proto {
            const SUBKEY: u8 = $key;
        }
    };
}

guild_config!(AutoConfig, 0_u8);
guild_config!(ModerationConfig, 1_u8);
guild_config!(LoggingConfig, 2_u8);
guild_config!(VerificationConfig, 3_u8);
guild_config!(MusicConfig, 4_u8);
guild_config!(AnnouncementConfig, 5_u8);
guild_config!(RoleConfig, 6_u8);
