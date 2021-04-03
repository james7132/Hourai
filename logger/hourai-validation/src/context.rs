use hourai::models::guild::Member;

#[derive(Debug, Clone)]
pub enum VerificationReason {
    Approval(String),
    Rejection(String),
}

impl VerificationReason {
    pub fn is_approval(&self) -> bool {
        self.approval_reason().is_some()
    }

    pub fn is_rejection(&self) -> bool {
        self.rejection_reason().is_some()
    }

    pub fn approval_reason(&self) -> Option<&str> {
        match self {
            VerificationReason::Approval(reason) => Some(reason.as_str()),
            _ => None,
        }
    }

    pub fn rejection_reason(&self) -> Option<&str> {
        match self {
            VerificationReason::Rejection(reason) => Some(reason.as_str()),
            _ => None,
        }
    }
}

pub struct VerificationContext {
    member: Member,
    reasons: Vec<VerificationReason>,
}

impl VerificationContext {
    pub fn new(member: Member) -> Self {
        Self {
            member,
            reasons: Vec::new(),
        }
    }

    pub fn member(&self) -> &Member {
        &self.member
    }

    pub fn add_approval_reason(&mut self, reason: impl Into<String>) {
        self.reasons
            .push(VerificationReason::Approval(reason.into()));
    }

    pub fn add_rejection_reason(&mut self, reason: impl Into<String>) {
        self.reasons
            .push(VerificationReason::Rejection(reason.into()));
    }

    pub fn add_reason(&mut self, reason: VerificationReason) {
        self.reasons.push(reason);
    }

    pub fn is_approved(&self) -> bool {
        self.reasons.last().map(|r| r.is_approval()).unwrap_or(true)
    }

    pub fn approval_reasons(&self) -> impl Iterator<Item = &str> {
        self.reasons.iter().filter_map(|r| r.approval_reason())
    }

    pub fn rejection_reasons(&self) -> impl Iterator<Item = &str> {
        self.reasons.iter().filter_map(|r| r.approval_reason())
    }
}
