use sqlx::{
    database::{HasArguments, HasValueRef},
    encode::IsNull,
    types::Type,
    Database, Decode, Encode,
};
use std::error::Error;

/// Wrapper for writing and storing Protocol Buffers to a SQL table column.
#[derive(Debug)]
pub struct Protobuf<T: protobuf::Message>(pub T);

impl<T: protobuf::Message> Type<crate::SqlDatabase> for Protobuf<T> {
    fn type_info() -> <crate::SqlDatabase as Database>::TypeInfo {
        <[u8] as Type<crate::SqlDatabase>>::type_info()
    }
}

// Allow Protobuf to be used in query arguments.
impl<'q, T: protobuf::Message> Encode<'q, crate::SqlDatabase> for Protobuf<T> {
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
impl<'r, T: protobuf::Message> Decode<'r, crate::SqlDatabase> for Protobuf<T>
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

/// A wrapper to to denote a 64-bit Unix timestamp with millisecond accuracy.
#[derive(Clone, Copy, Debug)]
pub struct UnixTimestamp(pub i64);

impl UnixTimestamp {
    pub fn now() -> Self {
        let ts = std::time::SystemTime::now()
            .duration_since(std::time::SystemTime::UNIX_EPOCH)
            .expect("It's past 01/01/1970. This should be a positive value.")
            .as_millis() as i64;
        Self(ts)
    }
}

impl From<u64> for UnixTimestamp {
    fn from(value: u64) -> Self {
        Self(value as i64)
    }
}

impl From<UnixTimestamp> for i64 {
    fn from(value: UnixTimestamp) -> Self {
        value.0 as i64
    }
}

impl Type<crate::SqlDatabase> for UnixTimestamp {
    fn type_info() -> <crate::SqlDatabase as Database>::TypeInfo {
        <i64 as Type<crate::SqlDatabase>>::type_info()
    }
}

impl<'q> Encode<'q, crate::SqlDatabase> for UnixTimestamp {
    fn encode_by_ref(
        &self,
        buf: &mut <crate::SqlDatabase as HasArguments<'q>>::ArgumentBuffer,
    ) -> IsNull {
        let ts = self.0 as i64;
        <i64 as Encode<'q, crate::SqlDatabase>>::encode_by_ref(&ts, buf);
        IsNull::No
    }

    fn produces(&self) -> Option<<crate::SqlDatabase as Database>::TypeInfo> {
        // `produces` is inherently a hook to allow database drivers to produce value-dependent
        // type information; if the driver doesn't need this, it can leave this as `None`
        let ts = self.0 as i64;
        <i64 as Encode<'q, crate::SqlDatabase>>::produces(&ts)
    }
}

impl<'r> Decode<'r, crate::SqlDatabase> for UnixTimestamp
where
    &'r str: Decode<'r, crate::SqlDatabase>,
{
    fn decode(
        value: <crate::SqlDatabase as HasValueRef<'r>>::ValueRef,
    ) -> Result<UnixTimestamp, Box<dyn Error + 'static + Send + Sync>> {
        let ts = <i64 as Decode<'r, crate::SqlDatabase>>::decode(value)?;
        Ok(UnixTimestamp(ts))
    }
}
