use flate2::{read::ZlibDecoder, write::ZlibEncoder, Compression};
use num_derive::FromPrimitive;
use num_traits::FromPrimitive;
use redis::{self, FromRedisValue, RedisWrite, ToRedisArgs};
use std::io::prelude::*;

/// The single byte compression mode header for values stored in Redis.
#[repr(u8)]
#[derive(FromPrimitive)]
enum CompressionMode {
    /// Uncompressed. The value is entirely uncompressed and can be used as is.
    Uncompressed = 0,
    /// Compressed with zlib. Default compression level: 6.
    Zlib = 1,
}

pub struct Compressed<T: ToRedisArgs + FromRedisValue>(pub T);

impl<T: ToRedisArgs + FromRedisValue> ToRedisArgs for Compressed<T> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut payload: Vec<Vec<u8>> = Vec::new();
        self.0.write_redis_args(&mut payload);
        for arg in payload {
            // The encoding shouldn't fail here due to writing to a in-memory buffer.
            let mut encoder = ZlibEncoder::new(Vec::new(), Compression::new(6));
            encoder.write_all(&arg).unwrap();
            let mut output = encoder.finish().unwrap();
            let compression_mode = if output.len() < arg.len() {
                CompressionMode::Zlib
            } else {
                output = arg.to_vec();
                CompressionMode::Uncompressed
            };
            output.insert(0, compression_mode as u8);
            out.write_arg(&output[..]);
        }
    }
}

impl<T: ToRedisArgs + FromRedisValue> FromRedisValue for Compressed<T> {
    fn from_redis_value(value: &redis::Value) -> redis::RedisResult<Self> {
        use redis::{ErrorKind, RedisError, Value};
        if let Value::Data(data) = value {
            let inner = if data.len() < 1 {
                T::from_redis_value(&value)?
            } else {
                let buf = &data[1..];
                let decompressed: Vec<u8> = match CompressionMode::from_u8(data[0]) {
                    Some(CompressionMode::Uncompressed) => buf.to_vec(),
                    Some(CompressionMode::Zlib) => {
                        let mut output: Vec<u8> = Vec::new();
                        ZlibDecoder::new(buf).read_to_end(&mut output)?;
                        output
                    }
                    // Default to returning the original payload if no match for the header is found
                    None => data.to_vec(),
                };
                T::from_redis_value(&Value::Data(decompressed))?
            };
            Ok(Self(inner))
        } else {
            Err(RedisError::from((
                ErrorKind::ResponseError,
                "Type cannot be compressed",
                format!("Invalid input: {:?}", value),
            )))
        }
    }
}
