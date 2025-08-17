use protobuf::Message;
use sqlx::{
    database::{HasArguments, HasValueRef},
    encode::IsNull,
    types::Type,
    Database, Decode, Encode,
};
use std::error::Error;
use std::ops::{Deref, DerefMut};

/// Wrapper for writing and storing Protocol Buffers to a SQL table column.
#[derive(Clone, Debug)]
pub struct Protobuf<T: Message>(pub T);

impl<T: Message> Type<crate::SqlDatabase> for Protobuf<T> {
    fn type_info() -> <crate::SqlDatabase as Database>::TypeInfo {
        <[u8] as Type<crate::SqlDatabase>>::type_info()
    }
}

// Allow Protobuf to be used in query arguments.
impl<'q, T: Message> Encode<'q, crate::SqlDatabase> for Protobuf<T> {
    fn encode_by_ref(
        &self,
        buf: &mut <crate::SqlDatabase as HasArguments<'q>>::ArgumentBuffer,
    ) -> IsNull {
        self.0
            .write_to_vec(buf)
            .expect("Should not be producing invalid Protobufs");
        IsNull::No
    }
}

// Allow Protobuf to be used in FromRow definitions.
impl<'r, T: Message> Decode<'r, crate::SqlDatabase> for Protobuf<T>
where
    &'r str: Decode<'r, crate::SqlDatabase>,
{
    fn decode(
        value: <crate::SqlDatabase as HasValueRef<'r>>::ValueRef,
    ) -> Result<Protobuf<T>, Box<dyn Error + 'static + Send + Sync>> {
        let bytes = <&'r [u8] as Decode<'r, crate::SqlDatabase>>::decode(value)?;
        let proto = T::parse_from_bytes(bytes)?;
        Ok(Protobuf(proto))
    }
}

impl<T: Message> From<T> for Protobuf<T> {
    fn from(value: T) -> Self {
        Self(value)
    }
}

impl<T: Message> Deref for Protobuf<T> {
    type Target = T;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

impl<T: Message> DerefMut for Protobuf<T> {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.0
    }
}
