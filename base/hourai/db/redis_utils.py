import aioredis
import asyncio

async def redis_transaction(redis, txn_func):
    try:
        tr = redis.multi_exec()
        futs = list(txn_func(tr))
        results = await tr.execute()
        assert results == (await asyncio.gather(*futs))
        return results
    except aioredis.MultiExecError:
        log.exception('Failure in Redis Transaction:')
        raise
