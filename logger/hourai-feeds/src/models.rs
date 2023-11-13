use anyhow::Result;
use hourai::http::error::ErrorType as HttpErrorType;
use hourai::models::{
    channel::message::embed::Embed,
    id::{marker::ChannelMarker, Id},
};
use hourai_sql::{
    sql_types::chrono::{DateTime, Utc},
    Executor, SqlQuery, SqlQueryAs,
};
use tracing::error;

#[derive(Debug)]
pub struct Post {
    channel_ids: Vec<Id<ChannelMarker>>,
    content: Option<String>,
    embed: Option<Embed>,
}

impl Post {
    pub fn broadcast(self, client: crate::Client) -> Result<()> {
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
        client: crate::Client,
        channel_id: Id<ChannelMarker>,
        content: Option<String>,
        embed: Option<Embed>,
    ) -> Result<()> {
        let embeds = embed.into_iter().collect::<Vec<_>>();
        let request = client
            .http
            .create_message(channel_id)
            .content(content.as_deref().unwrap_or(""))?
            .embeds(&embeds)?;

        if let Err(err) = request.await {
            if let HttpErrorType::Response { status, .. } = err.kind() {
                if status.get() == 404 {
                    client
                        .sql
                        .execute(Feed::delete_feed_channel(channel_id))
                        .await?;
                    tracing::warn!(
                        "Deleted channel {} from the database. No longer postable.",
                        channel_id
                    );
                }
            }
            Err(anyhow::anyhow!(err))
        } else {
            Ok(())
        }
    }
}

#[derive(sqlx::FromRow)]
pub struct Feed {
    pub id: i32,
    pub source: String,
    pub last_updated: DateTime<Utc>,
    pub channel_ids: Vec<i64>,
}

impl Feed {
    /// Fetches a page of feeds with a given type
    pub fn fetch_page<'a>(
        feed_type: impl Into<String>,
        count: u64,
        offset: u64,
    ) -> SqlQueryAs<'a, Self> {
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
        .bind(feed_type.into())
        .bind(count as i64)
        .bind(offset as i64)
    }

    /// Creates a query to update the database
    pub fn update<'a>(&self, latest_time: impl Into<DateTime<Utc>>) -> SqlQuery<'a> {
        sqlx::query("UPDATE feeds SET last_updated = $1 WHERE id = $2")
            .bind(latest_time.into())
            .bind(self.id)
    }

    pub fn delete_feed_channel<'a>(channel_id: Id<ChannelMarker>) -> SqlQuery<'a> {
        sqlx::query("DELETE FROM feed_channels WHERE channel_id = $1").bind(channel_id.get() as i64)
    }

    pub fn make_post(&self, content: Option<String>, embed: Option<Embed>) -> Post {
        Post {
            channel_ids: self
                .channel_ids
                .iter()
                .map(|id| Id::new(*id as u64))
                .collect(),
            content,
            embed,
        }
    }

    pub async fn delete(&self, sql: &hourai_sql::SqlPool) -> Result<()> {
        let mut txn = sql.begin().await?;
        sqlx::query("DELETE FROM feed_channels WHERE feed_id = $1")
            .bind(self.id)
            .execute(&mut txn)
            .await?;
        sqlx::query("DELETE FROM feeds WHERE id = $1")
            .bind(self.id)
            .execute(&mut txn)
            .await?;
        txn.commit().await?;
        tracing::info!(
            "Deleted feed (ID: {}) from the database. No longer readable.",
            self.id
        );
        Ok(())
    }
}
