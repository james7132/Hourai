import asyncio
import discord
import logging
import texttable
import typing
from datetime import datetime
from discord.ext import commands, tasks
from hourai.db import escalation_history, models
from hourai.utils import fake, checks, format


log = logging.getLogger(__name__)


async def deprecation_notice(ctx, alt):
    await ctx.send(
        f"This command is deprecated and will be removed soon. Please use the "
        f"`/{alt}` slash command instead. For more information on how to use "
        f"Hourai's Slash Commands, please read the documentation here: "
        f"https://docs.hourai.gg/Slash-Commands.")


class EscalationMixin:

    @commands.group(name='escalate', invoke_without_command=True)
    async def escalate(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "escalate up")

    @commands.command(name='deescalate')
    async def deescalate(self, ctx, reason: str, *users: discord.Member):
        await deprecation_notice(ctx, "escalate down")

    @escalate.command(name='history')
    async def escalate_history(self, ctx,
                               user: typing.Union[discord.Member, int]):
        await deprecation_notice(ctx, "escalate history")
