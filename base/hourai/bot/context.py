from __future__ import annotations
from hourai.db import proxies
from hourai.utils.replacement import StringReplacer
from hourai.utils.fake import FakeMessage
from discord.ext import commands

MAX_CONTEXT_DEPTH = 255


class HouraiContext(commands.Context):

    REPLACER = StringReplacer({
        '$author': lambda ctx: ctx.author.display_name,
        '$author_id': lambda ctx: ctx.author.id,
        '$author_mention': lambda ctx: ctx.author.mention,
        '$channel': lambda ctx: ctx.channel.name,
        '$channel_id': lambda ctx: ctx.channel.id,
        '$channel_mention': lambda ctx: ctx.channel.mention,
        '$server': lambda ctx: ctx.guild.name,
        '$server_id': lambda ctx: ctx.guild.id,
    })

    def __init__(self, **attrs):
        self.parent = attrs.pop('parent', None)
        self.depth = attrs.pop('depth', 1)
        super().__init__(**attrs)
        self.session = self.bot.create_storage_session()
        # TODO(james7132): Make this lazy
        self.guild_proxy = self.bot.create_guild_proxy(self.guild)

    async def __aenter__(self) -> HouraiContext:
        self.session.__enter__()
        return self

    async def __aexit__(self, exc_type, exc, traceback):
        self.session.__exit__(exc_type, exc, traceback)

    def substitute_content(self, repeats: int = 20) -> str:
        return self.REPLACER.substitute(self.content, context=self,
                                        repeats=repeats)

    @property
    def is_automated(self) -> bool:
        return isinstance(self.message, FakeMessage)

    @property
    def logger(self):
        return self.bot.logger

    def get_automated_context(self,
                              msg: discord.Message = None) -> HouraiContext:
        if self.depth > MAX_CONTEXT_DEPTH:
            raise RecursionError
        return self.bot.get_automated_context(message=msg or self.message,
                                              parent=self,
                                              depth=self.depth + 1)

    def get_ancestors(self):
        current_context = self.parent
        while current_context is not None:
            yield current_context
            current_context = current_context.parent
