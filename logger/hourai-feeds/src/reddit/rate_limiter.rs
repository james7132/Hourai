use anyhow::{bail, Result};
use std::time::{Instant, Duration};
use std::str::FromStr;

#[derive(Debug)]
pub struct RateLimiter {
    used: u64,
    remaining: u64,
    reset_time: Instant,
}

impl Default for RateLimiter {
    fn default() -> Self {
        Self {
            used: 0,
            remaining: 0,
            reset_time: Instant::now(),
        }
    }
}

impl RateLimiter {
    pub async fn wait(&mut self) {
        let now = Instant::now();
        if now >= self.reset_time {
            return;
        }
        let diff = (self.reset_time - now).div_f64(self.remaining as f64);
        tracing::debug!("Waiting {} seconds to keep the rate limit steady. Remaining requests: {}",
                        diff.as_secs_f64(), self.remaining);
        tokio::time::sleep(diff).await;
    }

    pub fn update(&mut self, response: &reqwest::Response) {
        let headers = response.headers();
        if let Ok(used) = Self::get_header(response, "X-Ratelimit-Used") {
            self.used = used;
        }
        if let Ok(remaining) = Self::get_header(response, "X-Ratelimit-Remaining") {
            self.remaining = remaining;
        }
        if let Ok(reset) = Self::get_header(response, "X-Ratelimit-Remaining") {
            self.reset_time = Instant::now() + Duration::from_secs(reset);
        }
        tracing::debug!("Updated ratelimits: {:?}", self);
    }

    fn get_header<T>(response: &reqwest::Response, header: &str) -> Result<T>
        where T: FromStr, <T as FromStr>::Err: Sync + Send + std::error::Error + 'static
    {
        if let Some(header) = response.headers().get(header) {
            Ok(header.to_str()?.parse::<T>()?)
        } else {
            tracing::warn!("Could not find header '{}' in the response from Reddit.", header);
            bail!("Header not found.")
        }
    }
}
