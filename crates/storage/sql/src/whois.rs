use crate::{SqlPool, Username};
use anyhow::Result;
use hourai::{
    models::{
        guild::Member,
        id::{Id, marker::UserMarker},
        user::User,
    },
    util::whois,
};
use std::fmt::Write;
use twilight_util::builder::embed::EmbedBuilder;

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
        let mut description = String::from("```\n");
        for (i, username) in usernames.iter().enumerate() {
            if i > 0 {
                description.push('\n');
            }
            let date = username.timestamp.date_naive().format("%Y %b %d");
            if let Some(discriminator) = username.discriminator {
                write!(
                    description,
                    "{}  {}#{:04}",
                    date, username.name, discriminator
                )?;
            } else {
                write!(description, "{}  {}", date, username.name)?;
            }
        }
        description.push_str("\n```");
        Ok(Some(description))
    }
}
