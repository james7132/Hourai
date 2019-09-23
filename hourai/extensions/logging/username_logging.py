import asyncio
import discord
from discord.ext import commands
from datetime import datetime
from hourai import bot
from hourai.db import models

MAX_STORED_USERNAMES = 20

class UsernameLogging(bot.BaseCog):
    """ Cog for logging username changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_user_update(self, before, after):
        if before.name == after.name:
            return
        assert before.id == after.id
        await self.log_username_change(after)

    @commands.Cog.listener()
    async def on_message(self, msg):
        await self.log_username_change(msg.author)

    @commands.Cog.listener()
    async def on_member_join(self, member):
        await self.log_username_change(member)

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        await self.log_username_change(member)

    @commands.Co
    g.listener()
    async def on_member_ban(self, guild, user):
        await self.log_username_changes(user)

    @commands.Cog.listener()
    async def on_member_unban(self, guild, user):
        await self.log_username_changes(user)

    @commands.Cog.listener()
    async def on_group_join(self, group, user):
        await self.log_username_changes(user)

    @commands.Cog.listener()
    async def on_group_remove(self, group, user):
        await self.log_username_changes(user)

    async def log_all_member_changes(self, guild):
        if not guild.chunked:
            await guild.request_offline_members(guild)
        await asyncio.gather(*[
            self.log_username_change(member) for member in guild.members
        ])

    async def log_username_change(self, user):
        async with self.bot.create_storage_session() as session:
            usernames = session.query(models.Username) \
                               .filter_by(user_id=user.id) \
                               .order_by(models.Username.timestamp)
            usernames = list(usernames)
            if len(usernames) > 0 and usernames[0].name == user.name:
                return
            username = models.Username(user_id=user.id,
                                       timestamp=datetime.utcnow(),
                                       name=user.name,
                                       discriminator=user.discriminator)
            usernames.append(username)
            session.add(username)
            self.merge_usernames(usernames, session)
            if len(usernames) > MAX_STORED_USERNAMES:
                for name in usernames[:-MAX_STORED_USERNAMES]:
                    session.delete(name)
            session.commit()

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
                except:
                    pass
            else:
                before = username
