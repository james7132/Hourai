import logging
from functools import wraps


class BaseCog():

    def __init__(self, bot):
        self.bot = bot

        cog_name = self.__class__.__name__

        self.logger = logging.getLogger(cog_name)
        logging.info(f'Loaded cog: {cog_name}')


class ServerSpecificCog(BaseCog):

    def __init__(self, bot, server_id):
        super().__init__(bot)


def action_command(func):
    @wraps(func)
    async def wrapper(self, ctx, *args, **kwargs):
        actions = []
        async for action in func(self, ctx, *args, **kwargs):
            actions.append(action)
        await self.bot.execute_actions(actions)
        # TODO(james7132): Update this with a progress check dialogue
        await success(ctx)
    return wrapper


async def success(ctx):
    await ctx.send(config.SUCCESS_RESPONSE)
