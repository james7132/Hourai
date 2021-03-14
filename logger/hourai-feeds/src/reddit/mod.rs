mod auth;
mod models;
mod rate_limiter;

use self::models::*;
use crate::{models::*, Client};
use anyhow::Result;
use hourai::models::channel::embed::Embed;
use models::SubmissionListing;
use std::time::Duration;
use tracing::error;
use twilight_embed_builder::*;

const SUBREDDITS_PER_PAGE: u8 = 60;

pub struct RedditClient {
    http: reqwest::Client,
}

impl RedditClient {
    pub fn login() -> Self {
        Self {
            http: reqwest::Client::new(),
        }
    }

    pub async fn new(&self, subreddit: &str) -> Result<reqwest::Response> {
        let url = format!("https://reddit.com/r/{}/new.json?limit=100", subreddit);
        // TODO(james7132): Add OAuth and rate limiting
        Ok(self.http.get(url).send().await?)
    }
}

pub async fn start(client: Client) {
    tracing::debug!("Starting reddit feeds...");
    let reddit_client = RedditClient::login();
    //let mut cursor: u64 = 0;
    while !client.tx.is_closed() {
        //let feeds: Vec<Feed> = Feed::fetch_page("REDDIT", SUBREDDITS_PER_PAGE, cursor);
        let feeds: Vec<Feed> = vec![Feed {
            id: 0,
            feed_type: "REDDIT".to_owned(),
            source: "all".to_owned(),
            last_updated: 0,
            channel_ids: vec![1, 2, 3, 4, 5],
        }];
        for feed in feeds {
            let response = match reddit_client.new(feed.source.as_str()).await {
                Ok(response) => response,
                Err(err) => {
                    error!(
                        "Error while fetching posts from /r/{}: {:?}",
                        feed.source, err
                    );
                    continue;
                }
            };

            match make_posts(&feed, response).await {
                Ok(posts) => {
                    if let Err(err) = push_posts(&client, &feed, posts).await {
                        error!("Error while pushing Reddit posts: {}", err);
                    }
                },
                Err(err) => {
                    error!("Error while making Reddit posts: {}", err);
                    continue;
                },
            }
        }

        tokio::time::sleep(Duration::from_secs(2)).await;

        //if feeds.len() <= SUBREDDITS_PER_PAGE {
        //cursor = 0;
        //} else {
        //cursor += SUBREDDITS_PER_PAGE;
        //}
    }
}

async fn push_posts(client: &Client, feed: &Feed, posts: Vec<Post>) -> Result<()> {
    //let mut update_time = feed.last_updated;
    for post in posts {
        //if posts.created_at > feed.last_updated {
        let _ = client.tx.unbounded_send(post);
        //}
    }
    Ok(())
    //if update_time != feed.last_updated {
    //feed.update(update_time).exexute(&client.sql).await;
    //}
}

async fn make_posts(feed: &Feed, response: reqwest::Response) -> Result<Vec<Post>> {
    let mut text = response.text().await?;
    let submissions: SubmissionListing<'_> = simd_json::serde::from_str(text.as_mut_str())?;
    let posts = submissions
        .data
        .children
        .into_iter()
        .filter_map(|sub| make_post(feed, sub.data).ok())
        .collect();
    Ok(posts)
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
