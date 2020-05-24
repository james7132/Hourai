# Guild Specific Code

import asyncio
import discord
import logging
import texttable
from datetime import datetime
from discord.ext import commands
from hourai.cogs import GuildSpecificCog
from hourai.db import models, escalation_history
from hourai.utils import invite, checks, format

BANNED_GUILDS = {557153176286003221}


class GuildSpecific_TheGap(GuildSpecificCog):
    """ Guild specific code for The Gap, a server list server for Touhou related
    communities.
    """

    BIG_SERVER_SIZE = 250

    @commands.Cog.listener()
    async def on_message(self, msg):
        # Deletes any message that doesn't contain a server link.
        category = msg.channel.category
        if category is None or 'server' not in category.name.lower():
            return

        def on_error(e, t, tb):
            return logging.exception('Failed to get invite:')
        invites = await invite.get_all_discord_invites(
            self.bot, msg.content, on_error=on_error)
        invites = [inv for inv in invites if inv is not None]
        delete = len(invites) <= 0
        # If posted in #big-servers make sure it actually is big
        if 'big' in msg.channel.name:
            delete = delete or not any(
                inv.approximate_member_count >= self.BIG_SERVER_SIZE
                for inv in invites)
        delete = delete or any(inv.guild.id in BANNED_GUILDS
                               for inv in invites)
        if delete:
            await msg.delete()


class GuildSpecific_TouhouProject(GuildSpecificCog):

    def __init__(self, bot, guilds):
        super().__init__(bot, guilds)
        self.apply_pending_deescalations.start()

    def cog_unload(self):
        self.apply_pending_deescalations.cancel()

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
        guild = self.bot.get_guild(pending.guild_id)
        if guild is not None:
            history = escalation_history.UwerEscalationHistory(
                user=fake.FakeSnowflake(pending.user_id),
                guild=guild, session=session)
            reason = deesc.entry
            assert dees.action.HasField('reason')
            await history.apply_diff(guild.me, deesc.action.reason,
                                     dees.amount, execute=False)
        session.delete(pending)
        session.commit()

    def __query_pending_deescalations(self, session):
        now = datetime.utcnow()
        return session.query(models.PendingDeescalations) \
                      .filter(models.PendingDeescalations.expiration < now) \
                      .order_by(models.PendingDeescalations.expiration) \
                      .all()

    @apply_pending_deescalations.before_loop
    async def before_apply_pending_deescalations(self):
        await self.bot.wait_until_ready()

    @commands.group(name='escalate')
    @checks.is_moderator()
    async def escalate(self, ctx, reason: str, *, users: discord.Member):
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
    async def deescalate(self, ctx, reason: str, *, users: discord.Member):
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

    @commands.command(name='history')
    async def escalate_history(self, ctx, user: discord.Member):
        history = escalation_history.UserEscalationHistory(
            self.bot, user, ctx.guild)

        comps = [f"**Escalation History for {user.mention}**"]
        comps.append(self.__build_escalation_history_table(history))
        await ctx.send(format.vertical_list(comps))

    def __build_escalation_history_table(self, history):
        if len(history.entries) <= 0:
            return "```\nNo history of escalation events.\n```"
        columns = ('Date', 'Action', 'Authorizer', 'Level', 'Reason')

        table = texttable.Texttable()
        table.set_cols_align(["r"] * len(columns))
        table.set_cols_valign(["t"] + ["i"] * (len(columns) - 1))
        table.set_deco(texttable.Texttable.HEADER | texttable.Texttable.VLINES)
        table.header(columns)
        level = -1
        for entry in history.entries:
            level = max(-1, level + entry.level_delta)

            authorizer_name = entry.authorizer_name
            authorizer = history.guild.get_member(entry.authorizer.id)
            if authorizer is not None:
                authorizer_name = \
                        f"{authorizer.name}#{authorizer.discriminator}"

            table.add_row([str(entry.timestamp), entry.display_name,
                           authorizer_name, level, entry.action.reason])
        return f"```\n{table.render()}\n```"


__GUILD_COGS = {
    GuildSpecific_TheGap: {355145270029451264},
    GuildSpecific_TouhouProject: {163175631562080256, 208460178863947776},
}


def setup(bot):
    for cls, guilds in __GUILD_COGS.items():
        bot.add_cog(cls(bot, guilds=set(guilds)))
