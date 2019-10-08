import asyncio
import logging
import random
import collections
import itertools
from datetime import datetime
from discord.ext import commands, tasks
from hourai.cogs import BaseCog
from hourai.db.models import Username
from sqlalchemy import func
from sqlalchemy.exc import OperationalError

MAX_STORED_USERNAMES = 20


class UsernameLogging(BaseCog):
    """ Cog for logging username changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot
        # self.cleanup_username_histories.start()
        self.pending_ids = None
        self.offset = 0

    def cog_unload(self):
        # self.cleanup_username_histories.cancel()
        pass

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

    @tasks.loop(seconds=0.1)
    async def cleanup_username_histories(self):
        frame_size = 5000
        try:
            with self.bot.create_storage_session() as session:
                ids = session.query(Username.user_id) \
                             .group_by(Username.user_id) \
                             .offset(self.offset).limit(frame_size)

                ids = [x[0] for x in ids]

                if len(ids) <= 0:
                    self.offset = 0
                    return

                keys = lambda u: u.user_id

                usernames = session.query(Username) \
                                   .filter(Username.user_id.in_(ids)) \
                                   .all()
                usernames = list(usernames)
                usernames.sort(key=keys)
                for user_id, names in itertools.groupby(usernames, key=keys):
                    self.merge_names(names, session)

                if len(session.deleted) > 0:
                    self.log_changes(session)
                    session.commit()
                else:
                    self.offset += frame_size
        except Exception:
            self.bot.logger.exception('Exception while clearing histories:')

    @cleanup_username_histories.before_loop
    async def before_cleanup_username_histories(self):
        await self.bot.wait_until_ready()

    @commands.command()
    @commands.is_owner()
    async def refresh(self, ctx):
        async with ctx.typing():
            await asyncio.gather(*[self.log_username_change(user)
                for user in ctx.bot.users])
        await ctx.send(':thumbsup:')

    async def log_username_change(self, user):
        # Don't log system or webhook accounts
        if int(user.discriminator) == 0:
            return

        timestamp = datetime.utcnow()

        def create_username():
            return Username(user_id=user.id, name=user.name,
                            timestamp=timestamp,
                            discriminator=user.discriminator)
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
                    username = create_username()
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
