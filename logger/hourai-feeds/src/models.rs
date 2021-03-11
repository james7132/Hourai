use hourai::models::{id::ChannelId, channel::embed::Embed};
use hourai_sql::{SqlQuery, SqlQueryAs};
use tracing::error;

#[derive(Clone)]
pub struct Post {
    channel_id: ChannelId,
    content: String,
    embed: Embed,
}

impl Post {

    pub async fn push(self, client: hourai::http::Client) -> Result<()>{
        client.create_message(self.channel_id)
              .content(self.content)?
              .embed(self.embed)?
              .await?
    }

}

#[derive(sqlx::FromRow)]
pub struct Feed {
    id: i64,
    #[sqlx(rename = "type")]
    feed_type: String,
    source: String,
    last_updated: i64,
    channel_ids: Vec<i64>,
}

impl Feed {

    /// Fetches a page of feeds with a given type
    pub fn fetch_page<'a>(feed_type: String, count: u64, offset: u64) -> SqlQueryAs<'a, Self> {
        sqlx::query_as("SELECT \
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
                       OFFSET $3")
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

    /// Broadcasts the post to all of the feed's channels.
    pub async fn broadcast(&self, client: &hourai::http::Client, post: impl Into<Post>) {
        let post = post.into();
        let mut tasks: Vec<tokio::task::JoinHandle<()>> = Vec::new();
        for channel_id in self.channel_ids {
            let subclient = client.clone();
            let mut subpost = post.clone();
            subpost.channel_id = ChannelId(channel_id as u64);
            tasks.push(tokio::spawn(async move {
                if let Err(err) = subpost.push(subclient).await {
                    error!("Error while posting to channel {}: {:?}", channel_id, err);
                }
            }));
        }
        futures::future::join_all(tasks).await;
    }

}
