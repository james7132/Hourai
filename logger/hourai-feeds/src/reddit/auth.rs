use anyhow::Result;
use hourai::config::*;
use serde::Deserialize;
use std::time::{Duration, Instant};

pub struct RedditAuth {
    pub http: reqwest::Client,
    pub expiration: Instant,
    pub token: String,
    pub config: RedditConfig,
}

impl RedditAuth {
    pub async fn login(config: RedditConfig) -> Result<Self> {
        let http = reqwest::Client::builder()
            .user_agent(config.user_agent.clone())
            .build()?;
        let auth = Self::authorize(&http, &config).await?;
        Ok(Self {
            http,
            config,
            expiration: auth.expiration(),
            token: auth.access_token,
        })
    }

    pub async fn get_token(&mut self) -> Result<String> {
        if Instant::now() < self.expiration {
            Ok(self.token.clone())
        } else {
            tracing::debug!("Access token expired. Refreshing...");
            let auth = Self::authorize(&self.http, &self.config).await?;
            self.expiration = auth.expiration();
            self.token = auth.access_token;
            Ok(self.token.clone())
        }
    }

    async fn authorize(client: &reqwest::Client, config: &RedditConfig) -> Result<AuthResponse> {
        let mut response = client.post("https://www.reddit.com/api/v1/access_token")
            .basic_auth(config.client_id.clone(), Some(config.client_secret.clone()))
            .header(reqwest::header::CONTENT_TYPE, "application/x-www-form-urlencoded")
            .body("grant_type=https://oauth.reddit.com/grants/installed_client&device_id=DO_NOT_TRACK_THIS_DEVICE")
            .send()
            .await?
            .text()
            .await?;
        tracing::debug!("Refreshed token: {:?}", response);
        Ok(simd_json::serde::from_str(response.as_mut_str())?)
    }
}

#[derive(Deserialize)]
struct AuthResponse {
    pub access_token: String,
    pub expires_in: u64,
}

impl AuthResponse {
    fn expiration(&self) -> Instant {
        Instant::now() + Duration::from_secs(self.expires_in)
    }
}
