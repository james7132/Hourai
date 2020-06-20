import aioredis
import asyncio
import collections
import time
import logging


async def redis_transaction(redis, txn_func):
    try:
        tr = redis.multi_exec()
        futs = list(txn_func(tr))
        results = await tr.execute()
        assert results == (await asyncio.gather(*futs))
        return results
    except aioredis.MultiExecError:
        logging.exception('Failure in Redis Transaction:')
        raise
