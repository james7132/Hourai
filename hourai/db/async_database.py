import asyncio
import collections
import lmdb
import multiprocessing
from concurrent.futures import ThreadPoolExecutor

PendingWrite = collections.namedtuple('PendingWrite', 'db key value args kwargs')


class TransactionFailed(RuntimeError):
    pass


def open(*args, **kwargs):
    lmdb_env = lmdb.open(*args, **kwargs)
    return AsyncDatabase(lmdb_env)


def __flush_writes(env, writes):
    with env.begin(write=True) as txn:
        for write in writes.values():
            success = txn.put(write.key, write.value, *write.args, **write.kwargs)
            if not success:
                raise TransactionFailed()


class AsyncDatabase():

    def __init__(self, env):
        self.env = env
        cpu_count = multiprocessing.cpu_count()
        self.executor = ThreadPoolExecutor(max_workers=cpu_count)

    def __getattr__(self, attr):
        return getattr(self.env, attr)

    def begin(self, *args, **kwargs):
        return AsyncTransaction(self, *args, **kwargs)

    def close():
        pass

class AsyncTransaction():

    def __init__(self, db, *args, **kwargs):
        self.db = db
        self.txn = self.env.begin(write=False, *args, **kwargs)
        self.writes = {}

    def __getattr__(self, attr):
        return getattr(self.txn, attr)

    async def __aenter__(self):
        self.txn.__enter__()
        return self

    async def __aexit__(self, exc_type, exc, tb):
        if exc is None:
            await self.commit()
        self.txn.__exit__(exc_type, exc, tb)

    async def commit(self):
        if len(self.writes) <= 0:
            return
        args = (self.db.env, self.writes)
        await asyncio.run_in_executor(__flush_writes, args)

    def get(key, default=None, db=None):
        cache_key = self._get_cache_key(db, key)
        cached_value = self.writes.get(cache_key)
        if cached_value is not None:
            return cached_value
        return self.txn.get(key, default=default, db=db)

    def put(self, key, value, db=None, *args, **kwargs):
        cache_key = self._get_cache_key(db, key)
        write = PendingWrite(**locals())
        self.writes[cache_key] = write

    def _get_cache_key(self, db, key):
        return (db, key)
