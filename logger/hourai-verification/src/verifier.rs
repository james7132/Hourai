use crate::context::*;
use anyhow::Result;
use async_trait::async_trait;

pub type BoxedVerifier = Box<dyn Verifier + Sync + 'static>;

#[async_trait]
pub trait Verifier {
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

pub struct GenericVerifier {
    pub reason: VerificationReason,
    pub pred: Box<dyn Fn(&VerificationContext) -> Result<bool> + Sync + 'static>,
}

impl GenericVerifier {
    pub fn new_approver<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T,
    ) -> BoxedVerifier {
        Self::new(VerificationReason::Approval(reason.into()), approver)
    }

    pub fn new_rejector<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T,
    ) -> BoxedVerifier {
        Self::new(VerificationReason::Rejection(reason.into()), approver)
    }

    fn new<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: VerificationReason,
        approver: T,
    ) -> BoxedVerifier {
        Box::new(GenericVerifier {
            reason,
            pred: Box::new(approver),
        })
    }
}

#[async_trait]
impl Verifier for GenericVerifier {
    async fn verify(&self, ctx: &mut VerificationContext) -> Result<()> {
        let pred = &self.pred;
        if pred(ctx)? {
            ctx.add_reason(self.reason.clone());
        }
        Ok(())
    }
}
