mod auth;
mod models;
mod rate_limiter;

use self::models::*;
use crate::{models::*, Client};
use anyhow::Result;
use futures::lock::Mutex;
use hourai::models::channel::embed::Embed;
use hourai_sql::sql_types::chrono::{DateTime, NaiveDateTime, Utc};
use http::status::StatusCode;
use models::SubmissionListing;
use reqwest::Response;
use tracing::error;
use twilight_embed_builder::*;

const SUBREDDITS_PER_PAGE: u64 = 60;

pub struct RedditClient {
    auth: auth::RedditAuth,
    rate_limiter: Mutex<rate_limiter::RateLimiter>,
}

impl RedditClient {
    pub fn new(auth: auth::RedditAuth) -> Self {
        Self {
            auth: auth,
            rate_limiter: Mutex::new(rate_limiter::RateLimiter::default()),
        }
    }

    pub async fn get_new(&mut self, subreddit: &str) -> Result<Response> {
        {
            self.rate_limiter.lock().await.wait().await;
        }
        let token = self.auth.get_token().await?;
        let url = format!(
            "https://oauth.reddit.com/r/{}/new.json?limit=100",
            subreddit
        );
        let response = self.http().get(url).bearer_auth(token).send().await?;
        {
            self.rate_limiter.lock().await.update(&response);
        }
        Ok(response)
    }

    fn http(&self) -> &reqwest::Client {
        &self.auth.http
    }
}

pub async fn start(client: Client, config: hourai::config::RedditConfig) {
    tracing::info!("Starting reddit feeds...");
    let auth = auth::RedditAuth::login(config)
        .await
        .expect("Failed to authorize with Reddit");
    let mut reddit_client = RedditClient::new(auth);
    let mut cursor: u64 = 0;
    while !client.tx.is_closed() {
        let query = Feed::fetch_page("REDDIT", SUBREDDITS_PER_PAGE, cursor)
            .fetch_all(&client.sql)
            .await;
        let feeds = match query {
            Ok(feeds) => feeds,
            Err(err) => {
                tracing::error!("Error while fetching feeds from the SQL database: {}", err);
                client.tx.close_channel();
                return;
            }
        };

        for feed in &feeds {
            tracing::info!("Checking /r/{}", feed.source);
            let response = match reddit_client.get_new(feed.source.as_str()).await {
                Ok(response) => response,
                Err(err) => {
                    error!(
                        "Error while fetching posts from /r/{}: {:?}",
                        feed.source, err
                    );
                    continue;
                }
            };

            if let Err(err) = push_posts(&feed, response, &client).await {
                error!("Error while pushing Reddit posts: {}", err);
            }
        }

        if feeds.len() <= SUBREDDITS_PER_PAGE as usize {
            cursor = 0;
        } else {
            cursor += SUBREDDITS_PER_PAGE;
        }
    }
}

async fn push_posts(feed: &Feed, response: Response, client: &Client) -> Result<()> {
    // Handle errors from Reddit's API.
    match response.status() {
        StatusCode::NOT_FOUND => feed.delete(&client.sql).await?,
        StatusCode::FORBIDDEN => feed.delete(&client.sql).await?,
        code if code.is_success() => {}
        code if code.is_client_error() => {
            anyhow::bail!("Reddit returned a client error: {:?}", response)
        }
        code if code.is_server_error() => {
            anyhow::bail!("Reddit returned a server error: {:?}", response)
        }
        _ => anyhow::bail!("Unexpected response from Reddit: {:?}", response),
    }

    // Reddit reports creation time in seconds unix time.
    // Feeds are done with millisecond accuracy, so this ratio is to account for that difference.
    let mut update_time = feed.last_updated;
    let min_time = update_time;

    let mut text = response.text().await?;
    simd_json::serde::from_str::<SubmissionListing>(text.as_mut_str())?
        .data
        .children
        .into_iter()
        .rev()
        .map(|thing| thing.data)
        .filter(|sub| {
            let ts = sub.created_utc as i64;
            let timestamp = NaiveDateTime::from_timestamp(ts, 0);
            let post_time = DateTime::<Utc>::from_utc(timestamp, Utc);
            update_time = std::cmp::max(update_time, post_time);
            post_time > min_time
        })
        .filter_map(|sub| make_post(feed, sub).ok())
        .for_each(|post| {
            if let Err(err) = client.tx.unbounded_send(post) {
                error!("Error while sending post: {}", err);
            }
        });

    if update_time != feed.last_updated {
        feed.update(update_time).execute(&client.sql).await?;
    }
    Ok(())
}

fn make_post(feed: &Feed, source: Submission) -> Result<Post> {
    Ok(feed.make_post(
        Some(format!("New post in /r/{}", source.subreddit)),
        Some(make_embed(source)?),
    ))
}

fn make_embed(source: Submission) -> Result<Embed> {
    let mut builder = EmbedBuilder::new()
        .title(source.title)?
        .url(format!("https://reddit.com{}", source.permalink))
        .color(0xFF4301)?
        .author(
            EmbedAuthorBuilder::new()
                .name(format!("/u/{}", source.author))?
                .url(format!("https://reddit.com/u/{}", source.author)),
        );

    if let Some(flair_text) = source.link_flair_text {
        builder = builder.footer(EmbedFooterBuilder::new(flair_text)?);
    }

    if source.is_self {
        builder = builder.description(ellipsize(&source.selftext, 2000))?;
    } else if let Some(hint) = source.post_hint {
        if hint == "image" {
            builder = builder.image(ImageSource::url(source.url)?);
        } else {
            builder = builder.description(source.url)?;
        }
    } else {
        builder = builder.description(source.url)?;
    }

    Ok(builder.build()?)
}

fn ellipsize(input: &str, max_len: usize) -> String {
    assert!(max_len >= 3);
    let limit = max_len - 3;
    if input.chars().count() < limit {
        input.to_owned()
    } else {
        let end = input.char_indices().nth(limit).unwrap().0;
        format!("{}...", &input[0..end])
    }
}
