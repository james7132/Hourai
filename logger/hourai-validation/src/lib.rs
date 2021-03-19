#[macro_use]
extern crate lazy_static;

mod approvers;
mod context;
mod rejectors;

use self::context::VerificationContext;
use anyhow::Result;
use async_trait::async_trait;

pub type BoxedVerifier = Box<dyn Verifier + Sync + 'static>;

#[async_trait]
pub trait Verifier {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()>;
}

#[async_trait]
impl Verifier for Vec<BoxedVerifier> {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        for verifier in self.iter() {
            verifier.verify(ctx).await?;
        }
        Ok(())
    }
}

pub struct GenericVerifier {
    pub reason: context::VerificationReason,
    pub pred: Box<dyn Fn(&VerificationContext) -> Result<bool> + Sync + 'static>,
}

impl GenericVerifier {
    pub fn new_approver<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T,
    ) -> BoxedVerifier {
        Self::new(
            context::VerificationReason::Approval(reason.into()),
            approver,
        )
    }

    pub fn new_rejector<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T,
    ) -> BoxedVerifier {
        Self::new(
            context::VerificationReason::Rejection(reason.into()),
            approver,
        )
    }

    fn new<T: Fn(&VerificationContext) -> Result<bool> + Sync + 'static>(
        reason: context::VerificationReason,
        approver: T,
    ) -> BoxedVerifier {
        Box::new(GenericVerifier {
            reason: reason,
            pred: Box::new(approver),
        })
    }
}

#[async_trait]
impl Verifier for GenericVerifier {
    async fn verify(&self, ctx: &mut context::VerificationContext) -> Result<()> {
        let pred = &self.pred;
        if pred(ctx)? {
            ctx.add_reason(self.reason.clone());
        }
        Ok(())
    }
}
