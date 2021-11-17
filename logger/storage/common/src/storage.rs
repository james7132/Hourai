use either::Either;
use futures::{future::Future, stream::Stream};
use hourai::config::HouraiConfig;
use hourai_redis::RedisClient;
use hourai_sql::{
    database::HasStatement, Database, Error as SqlxError, Execute, Executor, SqlDatabase, SqlPool,
};
use sqlx_core::describe::Describe;
use std::{
    fmt::{Debug, Formatter},
    pin::Pin,
};

#[derive(Clone)]
pub struct Storage {
    sql: SqlPool,
    redis: RedisClient,
}

impl Storage {
    pub fn new(sql: SqlPool, redis: RedisClient) -> Self {
        Self { sql, redis }
    }

    pub async fn init(config: &HouraiConfig) -> Self {
        Self {
            sql: hourai_sql::init(config).await,
            redis: hourai_redis::init(config).await,
        }
    }

    #[inline(always)]
    pub fn sql(&self) -> &SqlPool {
        &self.sql
    }

    #[inline(always)]
    pub fn redis(&self) -> &RedisClient {
        &self.redis
    }
}

impl Debug for Storage {
    fn fmt(&self, f: &mut Formatter<'_>) -> std::fmt::Result {
        write!(f, "Storage")?;
        Ok(())
    }
}

impl<'c> Executor<'c> for &Storage {
    type Database = SqlDatabase;

    delegate! {
        to self.sql {
            fn fetch_many<'e, 'q, E>(
                self,
                query: E
            ) -> Pin<Box<dyn Stream<Item = Result<Either<<Self::Database as Database>::QueryResult, <Self::Database as Database>::Row>, SqlxError>> + Send + 'e>>
            where
                'q: 'e,
                'c: 'e,
                E: 'q + Execute<'q, Self::Database>;
            fn fetch_optional<'e, 'q, E>(
                self,
                query: E
            ) -> Pin<Box<dyn Future<Output = Result<Option<<Self::Database as Database>::Row>, SqlxError>> + Send + 'e>>
            where
                'q: 'e,
                'c: 'e,
                E: 'q + Execute<'q, Self::Database>;
            fn prepare_with<'e, 'q>(
                self,
                sql: &'q str,
                parameters: &'e [<Self::Database as Database>::TypeInfo]
            ) -> Pin<Box<dyn Future<Output = Result<<Self::Database as HasStatement<'q>>::Statement, SqlxError>> + Send + 'e>>
            where
                'q: 'e,
                'c: 'e;
            fn describe<'e, 'q: 'e>(
                self,
                sql: &'q str,
            ) -> Pin<Box<dyn Future<Output=Result<Describe<Self::Database>, SqlxError>> + Send + 'e>>
            where
                'c: 'e;
        }
    }
}
