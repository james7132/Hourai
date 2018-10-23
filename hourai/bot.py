import logging
import traceback
from discord.ext import commands

log = logging.getLogger(__name__)


class HouraiContext(commands.Context):
    pass


class Hourai(commands.AutoShardedBot):

    async def on_ready(self):
        log.info(f'Bot Ready: {self.user.name} ({self.user.id})')

    async def on_message(self, message):
        if message.author.bot:
            return
        await self.process_commands(message)

    async def process_commands(self, message):
        ctx = await self.get_context(message, cls=HouraiContext)

        if ctx.command is None:
            return

        await self.invoke(ctx)

    async def on_command_error(self, ctx, error):
        if isinstance(error, commands.NoPrivateMessage):
            await ctx.author.send('This command cannot be used in private messages.')
        elif isinstance(error, commands.DisabledCommand):
            await ctx.author.send('Sorry. This command is disabled and cannot be used.')
        elif isinstance(error, commands.CommandInvokeError):
            trace = traceback.format_exception(type(error), error,
                                               error.__traceback__)
            trace_str = '\n'.join(trace)
            log.error(f'In {ctx.command.qualified_name}:\n{trace_str}\n')

    async def _run_single_action(self, action):
        # TODO(james7132): Change these to be immutable
        # TODO(james7132): Log the action
        try:
            await action.commit(self)
            action.proto.status.code = ActionStatusCode.SUCCESS
        except Exception as e:
            action.proto.status.code = ActionStatusCode.ERROR
            action.proto.status.error_message = str(e)
        return action

    async def execute_actions(self, action):
        tasks = (_run_single_action(action) for action in actions)
        return await kasyncio.gather(*tasks)
