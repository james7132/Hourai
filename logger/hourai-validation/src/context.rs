use hourai::models::guild::Member;

#[derive(Debug, Clone)]
pub enum ValidationReason {
    Approval(String),
    Rejection(String),
}

impl ValidationReason {

    pub fn is_approval(&self) -> bool {
        self.approval_reason().is_some()
    }

    pub fn is_rejection(&self) -> bool {
        self.rejection_reason().is_some()
    }

    pub fn approval_reason(&self) -> Option<&str> {
        match self {
            ValidationReason::Approval(reason) => Some(reason.as_str()),
            _ => None,
        }
    }

    pub fn rejection_reason(&self) -> Option<&str> {
        match self {
            ValidationReason::Rejection(reason) => Some(reason.as_str()),
            _ => None,
        }
    }

}

pub struct ValidationContext {
    member: Member,
    reasons: Vec<ValidationReason>,
}

impl ValidationContext {

    pub fn new(member: Member) -> Self {
        Self {
            member: member,
            reasons: Vec::new(),
        }
    }

    pub fn member(&self) -> &Member {
        &self.member
    }

    pub fn add_approval_reason(&mut self, reason: impl Into<String>) {
        self.reasons.push(ValidationReason::Approval(reason.into()));
    }

    pub fn add_rejection_reason(&mut self, reason: impl Into<String>) {
        self.reasons.push(ValidationReason::Rejection(reason.into()));
    }

    pub fn add_reason(&mut self, reason: ValidationReason) {
        self.reasons.push(reason);
    }

    pub fn is_approved(&self) -> bool {
        self.reasons.last().map(|r| r.is_approval()).unwrap_or(true)
    }

    pub fn approval_reasons(&self) -> impl Iterator<Item=&str> {
        self.reasons.iter().filter_map(|r| r.approval_reason())
    }

    pub fn rejection_reasons(&self) -> impl Iterator<Item=&str> {
        self.reasons.iter().filter_map(|r| r.approval_reason())
    }

}
