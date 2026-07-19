use flate2::{Compression, read::ZlibDecoder, write::ZlibEncoder};
use num_derive::FromPrimitive;
use num_traits::FromPrimitive;
use redis::{ErrorKind, FromRedisValue, RedisError, RedisWrite, ToRedisArgs, Value};
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
    fn write_redis_args<W: ?Sized + RedisWrite>(&self, out: &mut W) {
        let mut payload: Vec<Vec<u8>> = Vec::new();
        self.0.write_redis_args(&mut payload);
        for arg in payload {
            // Pre-allocate header byte at index 0 to avoid O(N) memory shift on insert.
            let header = vec![0u8];
            let mut encoder = ZlibEncoder::new(header, Compression::new(6));
            let _ = encoder.write_all(&arg);
            let final_output = match encoder.finish() {
                Ok(mut output) if output.len() - 1 < arg.len() => {
                    output[0] = CompressionMode::Zlib as u8;
                    output
                }
                _ => {
                    let mut uncompressed = Vec::with_capacity(1 + arg.len());
                    uncompressed.push(CompressionMode::Uncompressed as u8);
                    uncompressed.extend(arg);
                    uncompressed
                }
            };
            out.write_arg(&final_output[..]);
        }
    }
}

impl<T: ToRedisArgs + FromRedisValue> FromRedisValue for Compressed<T> {
    fn from_redis_value(value: &redis::Value) -> redis::RedisResult<Self> {
        if let Value::Data(data) = value {
            let inner = if data.is_empty() {
                T::from_redis_value(value)?
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
