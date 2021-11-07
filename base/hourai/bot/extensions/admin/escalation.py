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


def require_escalation_config(ctx):
    if ctx.guild is None:
        raise commands.NoPrivateMessage()
    if not ctx.guild.config.moderation.HasField('escalation_ladder'):
        raise commands.CheckFailure(
            message="No escalation ladder has been configured for this server."
                    " Please configure one before running this command.")
    return True


class EscalationMixin:

    @commands.group(name='escalate', invoke_without_command=True)
    @checks.is_moderator()
    @commands.check(require_escalation_config)
    async def escalate(self, ctx, reason: str, *users: discord.Member):
        """Escalates a user and applies the appropriate moderation action.

        A history of escalation events can be seen with ~escalate history.
        See: ~help escalate history.

        Requires the escalation ladder to be configured properly.
        For more information:
        https://github.com/james7132/Hourai/wiki/Escalation-Ladder
        """
        async def escalate_user(user):
            history = escalation_history.UserEscalationHistory(
                self.bot, user, ctx.guild)
            try:
                result = await history.escalate(ctx.author, reason)
                response = (f"Action: {result.current_rung.display_name}. "
                            f"Next Time: {result.next_rung.display_name}. ")
                expiration = 'Expiration: Never'
                if result.expiration is not None:
                    expiration = f'Expiration: {str(result.expiration)}'
                return response + expiration
            except escalation_history.EscalationException as e:
                return 'Error: ' + e.message

        results = await asyncio.gather(*[escalate_user(u) for u in users])
        lines = [f"{u.name}: {res}" for u, res in zip(users, results)]
        await ctx.send("\n".join(lines))

    @commands.command(name='deescalate')
    @checks.is_moderator()
    @commands.check(require_escalation_config)
    async def deescalate(self, ctx, reason: str, *users: discord.Member):
        """Deesclates a user and applies the appropriate moderation action.

        A history of escalation events can be seen with ~escalate history.
        See: ~help escalate history.

        Requires the escalation ladder to be configured properly.
        For more information:
        https://github.com/james7132/Hourai/wiki/Escalation-Ladder
        """
        async def escalate_user(user):
            history = escalation_history.UserEscalationHistory(
                self.bot, user, ctx.guild)
            try:
                result = await history.deescalate(ctx.author, reason)
                response = (f"Action: Deescalation. "
                            f"Next Time: {result.next_rung.display_name}. ")
                expiration = 'Expiration: Never'
                if result.expiration is not None:
                    expiration = f'Expiration: {str(result.expiration)}'
                return response + expiration
            except escalation_history.EscalationException as e:
                return 'Error: ' + e.message

        results = await asyncio.gather(*[escalate_user(u) for u in users])
        lines = [f"{u.name}: {res}" for u, res in zip(users, results)]
        await ctx.send("\n".join(lines))

    @escalate.command(name='history')
    async def escalate_history(self, ctx,
                               user: typing.Union[discord.Member, int]):
        """Deesclates a user and applies the appropriate moderation action.

        A history of escalation events can be seen with ~escalate history.
        See: ~help escalate history.

        Requires the escalation ladder to be configured properly.
        For more information:
        https://github.com/james7132/Hourai/wiki/Escalation-Ladder
        """
        name = str(user)
        user = fake.FakeSnowflake(user) if isinstance(user, int) else user
        history = escalation_history.UserEscalationHistory(
            self.bot, user, ctx.guild)

        comps = [f"**Escalation History for {name}**"]
        comps.append(await self.__build_escalation_history_table(history))
        await ctx.send(format.vertical_list(comps))

    async def __build_escalation_history_table(self, history):
        if len(history.entries) <= 0:
            return "```\nNo history of escalation events.\n```"
        columns = ('Date', 'Action', 'Authorizer', 'Level', 'Reason')

        table = texttable.Texttable(max_width=160)
        table.set_cols_align(["r"] * len(columns))
        table.set_cols_valign(["t"] + ["i"] * (len(columns) - 1))
        table.set_deco(texttable.Texttable.HEADER | texttable.Texttable.VLINES)
        table.header(columns)
        level = 0
        for entry in history.entries:
            level = max(-1, level + entry.level_delta)

            authorizer_name = entry.authorizer_name
            authorizer = await self.bot.get_member_async(
                    history.guild, entry.authorizer_id)
            if authorizer is not None:
                authorizer_name = \
                        f"{authorizer.name}#{authorizer.discriminator}"

            reasons = set(a.reason for a in entry.action.action
                          if a.HasField('reason'))
            reason = '; '.join(reasons)
            timestamp = entry.timestamp.strftime("%b %d %Y %H:%M")

            table.add_row([timestamp, entry.display_name, authorizer_name,
                           level, reason])
        return f"```\n{table.draw()}\n```"
