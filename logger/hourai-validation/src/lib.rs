#[macro_use]
extern crate lazy_static;

mod context;
mod approvers;
mod rejectors;

use anyhow::Result;
use self::context::ValidationContext;
use async_trait::async_trait;

pub type BoxedValidator = Box<dyn Validator + Sync + 'static>;

#[async_trait]
pub trait Validator {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()>;
}

#[async_trait]
impl Validator for Vec<BoxedValidator> {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        for validator in self.iter() {
            validator.validate(ctx).await?;
        }
        Ok(())
    }
}

pub struct GenericValidator {
    pub reason: context::ValidationReason,
    pub pred: Box<dyn Fn(&ValidationContext) -> Result<bool> + Sync + 'static>,
}

impl GenericValidator {

    pub fn new_approver<T: Fn(&ValidationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T
    ) -> BoxedValidator {
        Self::new(context::ValidationReason::Approval(reason.into()), approver)
    }

    pub fn new_rejector<T: Fn(&ValidationContext) -> Result<bool> + Sync + 'static>(
        reason: impl Into<String>,
        approver: T
    ) -> BoxedValidator {
        Self::new(context::ValidationReason::Rejection(reason.into()), approver)
    }

    fn new<T: Fn(&ValidationContext) -> Result<bool> + Sync + 'static>(
        reason: context::ValidationReason,
        approver: T
    ) -> BoxedValidator {
        Box::new(GenericValidator {
            reason: reason,
            pred: Box::new(approver),
        })
    }

}


#[async_trait]
impl Validator for GenericValidator {
    async fn validate(&self, ctx: &mut context::ValidationContext) -> Result<()> {
        let pred = &self.pred;
        if pred(ctx)? {
            ctx.add_reason(self.reason.clone());
        }
        Ok(())
    }
}
