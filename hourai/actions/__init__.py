import asyncio
import copy
import collections
import hourai.db as db
import hourai.actions.action_factory as action_factory


ActionContext = collecitons.namedtuple('ActionContext',
                                       ['action', 'bot', 'proto', 'txn'])


class ActionRunner():

    def __init__(self, bot, executors, preprocessors=None, validators=None):
        self.bot = bot
        self.executors = executors
        self.preprocessors = preprocessors or []
        self.validators = validators or []

    async def run(self, *action_protos):
        with db.lmdb_env.begin(write=True) as txn:
            protos = (copy.deepcopy(proto) for proto in action_protos)
            protos = (self._preprocess_proto(proto, txn) for proto in protos)
            contexts = []
            for proto in protos:
                self._validate_proto(proto)
                context = ActionContext(
                    bot=self.bot,
                    action=action_factory.create(proto),
                    proto=proto,
                    txn=txn,
                )
                actions.append(context)
            for executor in self.executors:
                await self._run_executor(executor, actions)

    async def _run_executor(executor, contexts):
        async def execute(context):
            try:
                await executor(context)
            except Exception as e:
                log.exception(f'Error in action execution: {e}')
        await asyncio.gather(*[execute(context) for context in context])


# Executors

async def execute_action(ctx):
    await ctx.action.commit(ctx.bot)


async def log_action(ctx):
    txn = db.action_logs.transaction(ctx.txn)
    txn.put(proto)


async def schedule_undo(ctx):
    txn = db.scheduled_actions.transaction(ctx.txn)
    # TODO(james7132): Implement
