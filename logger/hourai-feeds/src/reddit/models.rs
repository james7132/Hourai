use serde::Deserialize;

pub type SubmissionListing<'a> = Thing<'a, Listing<'a, Thing<'a, Submission<'a>>>>;

#[derive(Debug, Deserialize)]
pub struct Thing<'a, T> {
    pub kind: &'a str,
    pub data: T,
}

#[derive(Debug, Deserialize)]
pub struct Listing<'a, T> {
    pub modhash: &'a str,
    pub children: Vec<T>,
}

#[derive(Debug, Deserialize)]
pub struct Submission<'a> {
    pub title: &'a str,
    pub author: &'a str,
    pub subreddit: &'a str,
    pub is_self: bool,
    pub selftext: &'a str,
    pub permalink: &'a str,
    pub url: &'a str,
    pub post_hint: Option<&'a str>,
    pub link_flair_text: Option<&'a str>,
    pub created_utc: f64,
}
