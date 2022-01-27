use crate::{SqlPool, Username};
use anyhow::Result;
use hourai::{
    models::{
        guild::Member,
        id::{marker::UserMarker, Id},
        user::User,
    },
    util::whois,
};
use twilight_embed_builder::EmbedBuilder;

const USERNAME_LIMIT: u64 = 20;

pub async fn user(sql: &SqlPool, user: &User) -> Result<EmbedBuilder> {
    let description = build_description(sql, user.id).await?;
    let mut builder = whois::user(user)?;
    if let Some(description) = description {
        builder = builder.description(description);
    }
    Ok(builder)
}

pub async fn member(sql: &SqlPool, member: &Member) -> Result<EmbedBuilder> {
    let description = build_description(sql, member.user.id).await?;
    let mut builder = whois::member(member)?;
    if let Some(description) = description {
        builder = builder.description(description);
    }
    Ok(builder)
}

async fn build_description(sql: &SqlPool, user_id: Id<UserMarker>) -> Result<Option<String>> {
    let usernames = Username::fetch(user_id, Some(USERNAME_LIMIT))
        .fetch_all(sql)
        .await?;
    if usernames.len() <= 1 {
        Ok(None)
    } else {
        Ok(Some(format!(
            "```\n{}\n```",
            usernames
                .into_iter()
                .map(|username| {
                    let date = username.timestamp.date().format("%Y %b %d");
                    let name = if let Some(discriminator) = username.discriminator {
                        format!("{}#{:04}", username.name, discriminator)
                    } else {
                        username.name
                    };
                    format!("{}  {}", date, name)
                })
                .collect::<Vec<String>>()
                .join("\n")
        )))
    }
}
