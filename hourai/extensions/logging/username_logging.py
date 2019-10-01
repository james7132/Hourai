import asyncio
import logging
import random
from datetime import datetime
from discord.ext import commands
from hourai.cogs import BaseCog
from hourai.db import models
from sqlalchemy.exc import OperationalError

MAX_STORED_USERNAMES = 20


class UsernameLogging(BaseCog):
    """ Cog for logging username changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_user_update(self, before, after):
        if before.name == after.name:
            return
        assert before.id == after.id
        self.bot.loop.create_task(self.log_username_change(after))

    @commands.Cog.listener()
    async def on_message(self, msg):
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

    async def log_all_member_changes(self, guild):
        if not guild.chunked:
            await guild.request_offline_members(guild)

        for member in guild.members:
            self.bot.loop.create_task(self.log_username_change(member))

    async def log_username_change(self, user):
        timestamp = datetime.utcnow()

        def create_username():
            return models.Username(user_id=user.id, name=user.name,
                                   timestamp=timestamp,
                                   discriminator=user.discriminator)
        logged = False
        backoff = 1
        while not logged:
            try:
                with self.bot.create_storage_session() as session:
                    usernames = session.query(models.Username) \
                                       .filter_by(user_id=user.id) \
                                       .order_by(models.Username.timestamp)
                    usernames = list(usernames)
                    if len(usernames) > 0 and usernames[0].name == user.name:
                        return
                    username = create_username()
                    usernames.append(username)
                    session.add(username)
                    self.merge_usernames(usernames, session)
                    if len(usernames) > MAX_STORED_USERNAMES:
                        for name in usernames[:-MAX_STORED_USERNAMES]:
                            session.delete(name)
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

    def merge_usernames(self, usernames, session):
        before = None
        for username in list(sorted(usernames, key=lambda u: u.timestamp)):
            if before is not None and before.name == username.name:
                before.timestamp = min(before.timestamp, username.timestamp)
                before.discriminator = (before.discriminator or
                                        username.discriminator)
                usernames.remove(username)
                try:
                    session.delete(username)
                except Exception:
                    pass
            else:
                before = username
