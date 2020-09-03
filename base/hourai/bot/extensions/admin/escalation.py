import asyncio
import discord
import logging
import texttable
from datetime import datetime
from discord.ext import commands, tasks
from hourai import utils
from hourai.db import escalation_history, models
from hourai.utils import fake, checks, format


log = logging.getLogger(__name__)


async def require_escalation_config(ctx):
    if ctx.guild_proxy is None:
        raise commands.NoPrivateMessage()
    cfg = await ctx.guild_proxy.config.get('moderation')
    if not cfg.HasField('escalation_ladder'):
        raise commands.CheckFailure(
            message="No escalation ladder has been configured for this server."
                    " Please configure one before running this command.")
    return True


class EscalationMixin:

    def __init__(self, bot):
        self.apply_pending_deescalations.start()
        super().__init__()

    def cog_unload(self):
        self.apply_pending_deescalations.cancel()
        super().cog_unload()

    @tasks.loop(seconds=1)
    async def apply_pending_deescalations(self):
        try:
            session = self.bot.create_storage_session()
            with session:
                for deesc in self.__query_pending_deescalations(session):
                    await self.__apply_pending_deescalation(session, deesc)
        except Exception:
            log.exception('Error in running pending deescalation:')

    async def __apply_pending_deescalation(self, session, deesc):
        guild = self.bot.get_guild(deesc.guild_id)
        if guild is not None:
            history = escalation_history.UserEscalationHistory(
                bot=self.bot,
                user=fake.FakeSnowflake(deesc.user_id),
                guild=guild, session=session)
            await history.apply_diff(guild.me, 'Automatic Deescalation',
                                     deesc.amount, execute=False)
        session.delete(deesc)
        session.commit()

    def __query_pending_deescalations(self, session):
        now = datetime.utcnow()
        return session.query(models.PendingDeescalation) \
                      .filter(models.PendingDeescalation.expiration < now) \
                      .order_by(models.PendingDeescalation.expiration) \
                      .all()

    @apply_pending_deescalations.before_loop
    async def before_apply_pending_deescalations(self):
        await self.bot.wait_until_ready()

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
    async def escalate_history(self, ctx, user: discord.Member):
        """Deesclates a user and applies the appropriate moderation action.

        A history of escalation events can be seen with ~escalate history.
        See: ~help escalate history.

        Requires the escalation ladder to be configured properly.
        For more information:
        https://github.com/james7132/Hourai/wiki/Escalation-Ladder
        """
        history = escalation_history.UserEscalationHistory(
            self.bot, user, ctx.guild)

        comps = [f"**Escalation History for {user.mention}**"]
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
            authorizer = await utils.get_member_async(
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
