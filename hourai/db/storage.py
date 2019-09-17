import asyncio
import aioredis
from hourai import config
from sqlalchemy import create_engine, orm, pool
from hourai.db import models

class Storage:
    """A generic interface for managing the remote storage services connected to
    the bot.
    """

    __slots__ = ['config', 'session_class', 'redis']

    def __init__(self, config_module=config):
        self.config = config_module
        self.session_class = None
        self.redis = None

    async def init(self):
        await asyncio.gather(
            self._init_sql_database(),
            self._init_redis()
        )

    async def _init_sql_database(self):
        engine = self._create_sql_engine()
        self.session_class = orm.sessionmaker(bind=engine)
        self.ensure_created()

    async def _init_redis(self):
        self.redis = await aioredis.create_redis_pool(
                self.config.REDIS_CONNECTION,
                loop=asyncio.get_event_loop())

    def create_session(self):
        return StorageSession(self)

    async def close(self):
        redis.close()

    def ensure_created(self, engine=None):
        engine = engine or self._create_sql_engine()
        models.Base.metadata.create_all(engine)

    def _create_sql_engine(self):
        return create_engine(self.config.DB_CONNECTION,
                             poolclass=pool.SingletonThreadPool,
                             connect_args={'check_same_thread': False})


class StorageSession:
    __slots__ = ['storage', 'db_session', 'redis', 'subitems']

    def __init__(self, storage):
        self.storage = storage
        self.db_session = storage.session_class()
        self.redis = storage.redis

        self.subitems = (self.db_session, self.redis)

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc, exc_type, tb):
        if exc is None:
            self.db_session.commit()
        else:
            self.db_session.rollback()
        self.db_session.close()

    def __getattr__(self, attr):
        for subitem in self.subitems:
            try:
                return getattr(subitem, attr)
            except AttributeError:
                pass
        raise AttributeError
