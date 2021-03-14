use anyhow::Result;
use hourai::models::{channel::embed::Embed, id::ChannelId};
use hourai_sql::{SqlQuery, SqlQueryAs};
use tracing::error;

#[derive(Debug)]
pub struct Post {
    channel_ids: Vec<ChannelId>,
    content: Option<String>,
    embed: Option<Embed>,
}

impl Post {
    pub fn broadcast(self, client: hourai::http::Client) -> Result<()> {
        for channel_id in self.channel_ids {
            let subclient = client.clone();
            let content = self.content.clone();
            let embed = self.embed.clone();
            tokio::spawn(async move {
                if let Err(err) = Self::push(subclient, channel_id, content, embed).await {
                    error!("Error while posting to channel {}: {:?}", channel_id, err);
                }
            });
        }
        Ok(())
    }

    async fn push(
        client: hourai::http::Client,
        channel_id: ChannelId,
        content: Option<String>,
        embed: Option<Embed>,
    ) -> Result<()> {
        let mut request = client.create_message(channel_id);
        if let Some(content) = content {
            request = request.content(content.as_str())?;
        }
        if let Some(embed) = embed {
            request = request.embed(embed.clone())?;
        }
        request.await?;
        Ok(())
    }
}

#[derive(sqlx::FromRow)]
pub struct Feed {
    pub id: i64,
    #[sqlx(rename = "type")]
    pub feed_type: String,
    pub source: String,
    pub last_updated: i64,
    pub channel_ids: Vec<i64>,
}

impl Feed {
    /// Fetches a page of feeds with a given type
    pub fn fetch_page<'a>(feed_type: String, count: u64, offset: u64) -> SqlQueryAs<'a, Self> {
        sqlx::query_as(
            "SELECT \
                           feeds.id, feeds.source, feeds.last_updated, \
                           array_agg(feed_channels.channel_id) as channel_ids \
                       FROM \
                           feeds \
                       INNER JOIN \
                           feed_channels ON feeds.id = feed_channels.feed_id \
                       WHERE \
                           feeds.type = $1\
                       GROUP BY
                           feeds.id \
                       LIMIT $2 \
                       OFFSET $3",
        )
        .bind(feed_type)
        .bind(count as i64)
        .bind(offset as i64)
    }

    /// Creates a query to update the database
    pub fn update<'a>(&self, latest_time: u64) -> SqlQuery<'a> {
        sqlx::query("UPDATE feeds SET feeds.last_updated = $1 WHERE feeds.id = $2")
            .bind(latest_time as i64)
            .bind(self.id)
    }

    pub fn make_post(&self, content: Option<String>, embed: Option<Embed>) -> Post {
        Post {
            channel_ids: self
                .channel_ids
                .iter()
                .map(|id| ChannelId(*id as u64))
                .collect(),
            content: content,
            embed: embed,
        }
    }
}
