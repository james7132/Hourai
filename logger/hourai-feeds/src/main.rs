mod models;

use hourai::{init, config};
use hourai_sql::SqlPool;
use twilight_embed_builder::*;
use tracing::error;
use mr_splashy_pants::Pants;

const SUBREDDITS_PER_PAGE: u8 = 60;

#[tokio::main]
async fn main() -> std::io::Result<()> {
    let config = config::load_config(config::get_config_path().as_ref());
    init::init(&config);

    let client = Client {
        http: init::http_client(&config).await,
        sql: hourai_sql::init(&config).await,
    };

    let mut cursor: u64 = 0;
    loop {
        let feeds: Vec<Feed> = Feed::fetch_page("REDDIT", SUBREDDITS_PER_PAGE, cursor);
        for feed in feeds {
            let posts = match reddit_client.new(&feed.source).await {
                Ok(posts) => posts,
                Err(err) => {
                    error!("Error while fetching posts from /r/{}: {:?}", feed.source, err);
                    continue;
                },
            };

            let subclient = client.clone();
            tokio::spawn(async move {
                // Await each broadcast to ensure that each is updated
                for post in posts {
                    feed.broadcast(&subclient, make_post(post)).await;
                }

                // TODO(james7132): Update last_updated here.
                if let Err(err) = feed.update(utc_now).execute(&subclient.sql).await {
                    error!("Error occured while updating feed {} in DB: {:?}", feed.id, err);
                }
            });
        }

        if feeds.len() <= SUBREDDITS_PER_PAGE {
            cursor = 0;
        } else {
            cursor += SUBREDDITS_PER_PAGE;
        }
    }
}

fn make_post(source: reddit::Post) -> Result<Post> {
    Post {
        channel_id: ChannelId(0),
        content: format!("New post in /r/{}", source.subreddit),
        embed: format!("New post in /r/{}", source.subreddit),
    }
}

#[derive(Clone)]
pub struct Client {
    http: hourai::http::Client,
    sql: SqlPool,
}
