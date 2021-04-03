use twilight_model::{
    guild::{Permissions, Role},
    id::RoleId,
};

#[derive(Clone, Debug, Eq, Hash, PartialEq)]
pub struct CachedRole {
    pub id: RoleId,
    pub color: u32,
    pub name: Box<str>,
    pub permissions: Permissions,
    pub position: i64,
}

impl From<&Role> for CachedRole {
    fn from(value: &Role) -> Self {
        Self {
            id: value.id,
            color: value.color,
            name: value.name.clone().into_boxed_str(),
            permissions: value.permissions,
            position: value.position,
        }
    }
}
