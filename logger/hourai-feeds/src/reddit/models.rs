use serde::Deserialize;

pub type SubmissionListing = Thing<Listing<Thing<Submission>>>;

#[derive(Debug, Deserialize)]
pub struct Thing<T> {
    pub kind: String,
    pub data: T,
}

#[derive(Debug, Deserialize)]
pub struct Listing<T> {
    pub modhash: String,
    pub children: Vec<T>,
}

#[derive(Debug, Deserialize)]
pub struct Submission {
    pub title: String,
    pub author: String,
    pub subreddit: String,
    pub is_self: bool,
    pub selftext: String,
    pub permalink: String,
    pub url: String,
    pub post_hint: Option<String>,
    pub link_flair_text: Option<String>,
    pub created_utc: f64,
}
