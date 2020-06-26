import asyncio
import logging
import random
from datetime import datetime
from discord.ext import commands
from hourai.bot import cogs
from hourai.utils import iterable
from hourai.db.models import Username
from sqlalchemy.exc import OperationalError

MAX_STORED_USERNAMES = 20


class UsernameLogging(cogs.BaseCog):
    """ Cog for logging username changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot
        self.pending_ids = None

    @commands.Cog.listener()
    async def on_user_update(self, before, after):
        if before.name == after.name:
            return
        assert before.id == after.id
        self.bot.loop.create_task(self.log_username_change(after))

    @commands.Cog.listener()
    async def on_message(self, msg):
        if msg.webhook_id is not None:
            return
        self.bot.loop.create_task(self.log_username_change(msg.author))

    @commands.Cog.listener()
    async def on_member_join(self, member):
        self.bot.loop.create_task(self.log_username_change(member))

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        self.bot.loop.create_task(self.log_username_change(member))

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        self.bot.loop.create_task(self.log_username_change(user))

    @commands.Cog.listener()
    async def on_member_unban(self, guild, user):
        self.bot.loop.create_task(self.log_username_change(user))

    @commands.Cog.listener()
    async def on_group_join(self, group, user):
        self.bot.loop.create_task(self.log_username_change(user))

    @commands.Cog.listener()
    async def on_group_remove(self, group, user):
        self.bot.loop.create_task(self.log_username_change(user))

    @commands.Cog.listener()
    async def on_guild_join(self, guild):
        members = guild.fetch_members(limit=None)
        async for chunk in iterable.chunk_async(members, chunk_size=10):
            await asyncio.gather(
                *[self.log_username_change(user) for user in chunk])

    @commands.command()
    @commands.is_owner()
    async def refresh(self, ctx):
        async with ctx.typing():
            async for chunk in iterable.chunk(ctx.bot.users, chunk_size=10):
                await asyncio.gather(
                    *[self.log_username_change(user) for user in chunk])
        await ctx.send(':thumbsup:')

    async def log_username_change(self, user):
        # Don't log system or webhook accounts
        if int(user.discriminator) == 0:
            return

        timestamp = datetime.utcnow()

        logged = False
        backoff = 1
        while not logged:
            try:
                with self.bot.create_storage_session() as session:
                    usernames = session.query(Username) \
                                       .filter_by(user_id=user.id) \
                                       .order_by(Username.timestamp.desc())
                    usernames = list(usernames)
                    if any(n.name == user.name for n in usernames):
                        return
                    username = Username(user_id=user.id, name=user.name,
                                        timestamp=timestamp,
                                        discriminator=user.discriminator)
                    usernames.append(username)
                    filtered = self.merge_names(usernames, session)
                    if username in filtered:
                        session.add(username)
                    self.log_changes(session)
                    session.commit()
                logged = True
            except OperationalError:
                msg = f'OperationalError: Retrying in {backoff} seconds.'
                logging.error(msg)
                delta = (random.random() - 0.5) / 5
                await asyncio.sleep(backoff * (1 + delta))
                backoff *= 2
                if backoff >= 10:
                    raise

    def log_changes(self, session):
        if len(session.deleted) > 0:
            output = '\n'.join(f'Deleting: {str(n)}'
                               for n in session.deleted)
            self.bot.logger.info(output)
        if len(session.dirty) > 0:
            output = '\n'.join(f'Updating: {str(n)}'
                               for n in session.dirty)
            self.bot.logger.info(output)
        if len(session.new) > 0:
            output = '\n'.join(f'Adding: {str(n)}'
                               for n in session.new)
            self.bot.logger.info(output)

    def merge_names(self, names, session):
        names = list(names)
        if len(names) <= 1:
            return names
        names.sort(key=lambda u: u.timestamp, reverse=True)
        changed = True
        removal = set()
        while changed:
            removal.clear()
            for i, after in enumerate(names[:-1]):
                for j, before in enumerate(names[i+1:]):
                    if before.name != after.name or before is after:
                        continue
                    before.discriminator = (before.discriminator or
                                            after.discriminator)
                    session.add(before)
                    try:
                        session.delete(after)
                    except Exception:
                        pass
                    removal.add(i)
                    break
            changed = len(removal) > 0
            names = [u for idx, u in enumerate(names)
                     if idx not in removal]
        if len(names) > MAX_STORED_USERNAMES:
            # Assumes the ordering is maintained
            for name in names[MAX_STORED_USERNAMES:]:
                session.delete(name)
            names = names[:MAX_STORED_USERNAMES]
        return names
