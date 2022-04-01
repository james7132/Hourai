use crate::models::{
    guild::Member,
    user::{User, UserLike},
};
use anyhow::Result;
use twilight_util::builder::embed::{
    image_source::ImageSource, EmbedBuilder, EmbedFieldBuilder, EmbedFooterBuilder,
};
use twilight_util::snowflake::Snowflake;

fn timestamp_to_str(timestamp: i64) -> String {
    format!("<t:{}:R>", timestamp)
}

pub fn user(user: &User) -> Result<EmbedBuilder> {
    let thumbnail = ImageSource::url(user.avatar_url())?;
    let mut builder = EmbedBuilder::new()
        .title(format!("{}#{:04}", user.name, user.discriminator,))
        .thumbnail(thumbnail)
        .footer(EmbedFooterBuilder::new(format!("ID: {}", user.id)));

    if let Some(color) = user.accent_color {
        builder = builder.color(color as u32);
    }

    builder = builder.field(EmbedFieldBuilder::new(
        "Created on",
        timestamp_to_str(user.id.timestamp() / 1000),
    ));

    Ok(builder)
}

pub fn member(member: &Member) -> Result<EmbedBuilder> {
    let mut builder = user(&member.user)?;

    builder = builder.field(EmbedFieldBuilder::new(
        "Joined at",
        timestamp_to_str(member.joined_at.as_secs() as i64),
    ));

    if let Some(ts) = member.premium_since {
        builder = builder.field(EmbedFieldBuilder::new(
            "Boosting Since",
            timestamp_to_str(ts.as_secs() as i64),
        ));
    }

    Ok(builder)
}
