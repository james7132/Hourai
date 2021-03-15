use redis::{FromRedisValue, RedisError, RedisResult, RedisWrite, ToRedisArgs};

pub struct Protobuf<T: protobuf::Message>(pub T);

impl<T: protobuf::Message> Protobuf<T> {
    fn parse_protobuf(data: impl AsRef<[u8]>) -> RedisResult<Self> {
        match T::parse_from_bytes(data.as_ref()) {
            Ok(proto) => Ok(Self::from(proto)),
            Err(err) => Err(Self::convert_error(err)),
        }
    }

    fn convert_error(err: protobuf::error::ProtobufError) -> RedisError {
        use protobuf::error::ProtobufError;
        use redis::ErrorKind;
        match err {
            ProtobufError::IoError(io_err) => RedisError::from(io_err),
            general_err => RedisError::from((
                ErrorKind::ResponseError,
                "Failed to parse Protobuf",
                general_err.to_string(),
            )),
        }
    }
}

impl<T: protobuf::Message> From<T> for Protobuf<T> {
    fn from(value: T) -> Self {
        Self(value)
    }
}

impl<T: protobuf::Message> ToRedisArgs for Protobuf<T> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        out.write_arg(
            self.0
                .write_to_bytes()
                .expect("Should not be generating malformed Protobufs.")
                .as_slice(),
        );
    }
}

impl<T: protobuf::Message> FromRedisValue for Protobuf<T> {
    fn from_redis_value(value: &redis::Value) -> RedisResult<Self> {
        use redis::ErrorKind;
        match value {
            redis::Value::Data(data) => Self::parse_protobuf(data),
            val => Err(RedisError::from((
                ErrorKind::ResponseError,
                "Type incompatible with Protobufs",
                format!("Invalid input: {:?}", val),
            ))),
        }
    }
}
