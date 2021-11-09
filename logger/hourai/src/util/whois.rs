use crate::models::{
    guild::Member,
    user::{User, UserLike},
};
use anyhow::Result;
use chrono::{offset::TimeZone, Utc};
use twilight_embed_builder::{image_source::ImageSource, EmbedBuilder, EmbedFieldBuilder};
use twilight_util::snowflake::Snowflake;

pub fn user(user: &User) -> Result<EmbedBuilder> {
    let thumbnail = ImageSource::url(user.avatar_url())?;
    let mut builder = EmbedBuilder::new()
        .title(format!(
            "{}#{:04} ({})",
            user.name, user.discriminator, user.id
        ))
        .thumbnail(thumbnail);

    if let Some(color) = user.accent_color {
        builder = builder.color(color as u32);
    }

    let timestamp = Utc.timestamp(user.id.timestamp() / 1000, 0);
    builder = builder.field(EmbedFieldBuilder::new(
        "Joined At",
        format!("{}", timestamp),
    ));

    Ok(builder)
}

pub fn member(member: &Member) -> Result<EmbedBuilder> {
    let mut builder = user(&member.user)?;

    if let Some(ts) = member.joined_at {
        let timestamp = Utc.timestamp(ts.as_secs() as i64, 0);
        builder = builder.field(EmbedFieldBuilder::new(
            "Joined At",
            format!("{}", timestamp),
        ));
    }

    if let Some(ts) = member.premium_since {
        let timestamp = Utc.timestamp(ts.as_secs() as i64, 0);
        builder = builder.field(EmbedFieldBuilder::new(
            "Boosting Since",
            format!("{}", timestamp),
        ));
    }

    Ok(builder)
}
