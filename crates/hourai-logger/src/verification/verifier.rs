use super::context::*;
use anyhow::Result;
use async_trait::async_trait;

pub type BoxedVerifier = Box<dyn Verifier + Send + Sync + 'static>;

#[async_trait]
pub trait Verifier: Send + Sync {
    async fn verify(&self, ctx: &mut VerificationContext) -> Result<()>;
}

#[async_trait]
impl Verifier for Vec<BoxedVerifier> {
    async fn verify(&self, ctx: &mut VerificationContext) -> Result<()> {
        for verifier in self.iter() {
            verifier.verify(ctx).await?;
        }
        Ok(())
    }
}
