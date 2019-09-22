import asyncio
import aioredis
import enum
import logging
from concurrent.futures import ThreadPoolExecutor
from hourai import config
from sqlalchemy import create_engine, orm, pool
from . import models
from .caches import *
from .proto import *

log = logging.getLogger(__name__)

class StoragePrefix(enum.Enum):
    AUTO_CONFIG                 = 0
    MOD_CONFIG                  = 1
    LOGGING_CONFIG              = 2
    VALIDATION_CONFIG           = 3
    BANS                        = 4

def _make_prefixed_coder(prefix):
    return ChainCoder([IntCoder(), PrefixCoder(prefix)])

class Storage:
    """A generic interface for managing the remote storage services connected to
    the bot.
    """

    def __init__(self, config_module=config):
        self.config = config_module
        self.session_class = None
        self.redis = None
        self.executor = ThreadPoolExecutor
        for attr, _, _ in Storage._get_cache_configs():
            setattr(self, attr, None)

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
        # TODO(james7132): Move off of depending on Redis as a backing store
        await self._connect_to_redis()
        store = RedisStore(self.redis)
        for attr, proto, prefix in Storage._get_cache_configs():
            setattr(self, attr,
                    Cache(store,
                          key_coder=_make_prefixed_coder(bytes([prefix.value])),
                          value_coder=ProtobufCoder(proto)))

    async def _connect_to_redis(self):
        wait_time = 1.0
        max_wait_time = 60
        while True:
            try:
                self.redis = await aioredis.create_redis_pool(
                        self.config.REDIS_CONNECTION,
                        loop=asyncio.get_event_loop())
                break
            except aioredis.ReplyError:
                if wait_time >= max_wait_time:
                    raise
                log.exception(f'Failed to connect to Redis, backing off for '
                              f'{wait_time} seconds...')
                await asyncio.sleep(wait_time)
                wait_time *= 2

    @staticmethod
    def _get_cache_configs():
        return [
            ('auto_configs', AutoConfig, StoragePrefix.AUTO_CONFIG),
            ('moderation_configs', ModerationConfig, StoragePrefix.MOD_CONFIG),
            ('logging_configs', LoggingConfig, StoragePrefix.LOGGING_CONFIG),
            ('validation_configs', ValidationConfig,
                StoragePrefix.VALIDATION_CONFIG),
        ]

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

    @property
    def executor(self):
        return self.storage.executor

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc, exc_type, tb):
        if exc is None:
            self.db_session.commit()
        else:
            self.db_session.rollback()
        self.db_session.close()

    async def execute_query(self, callback, *args):
        return await asyncio.run_in_executor(self.executor, callback, *args)

    def __getattr__(self, attr):
        for subitem in self.subitems:
            try:
                return getattr(subitem, attr)
            except AttributeError:
                pass
        raise AttributeError
