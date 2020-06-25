import asyncio
from discord.ext import commands
from hourai.bot import cogs
from hourai.db import models
from hourai.utils import iterable


class RoleLogging(cogs.BaseCog):
    """ Cog for logging role changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_member_join(self, member):
        # TODO(james7132): Restore saved roles
        self.log_member_roles(member)

    @commands.Cog.listener()
    async def on_user_update(self, before, after):
        self.log_member_roles(member)

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        self.log_member_roles(member)

    @commands.Cogs.listener()
    async def on_guild_join(self, guild):
        await self.log_guild_roles(guild)

    @commands.Cogs.listener()
    async def on_guild_remove(self, guild):
        self.clear_guild(guild)

    async def log_all_guilds(self, guild):
        # FIXME: This will not scale to multiple processes/machines.
        await asyncio.gather(*[self.log_guild_roles(guild)
                               for guild in self.bot.guilds])

    async def log_guild_roles(self, guild):
        model = models.MemberRoles
        async for chunk in iterable.chunk_async(guild.fetch_members(limit=None),
                                                chunk_size=1000):
            roles = {member.id: self.create_member_roles(member)
                     for member in chunk}
            with self.bot.create_storage_session() as session:
                existing = session.query(model) \
                                  .filter_by(guild_id=guild.id) \
                                  .filter(model.user_id.in_(roles.keys())) \
                                  .all()

                for roles in existing:
                    # Only merge those posts which already exist in the database
                    session.merge(my_new_posts.pop(roles.member_id))

                # Only add those posts which did not exist in the database
                session.add_all(roles.values())
                session.commit()

    def log_member_roles(self, member):
        member_roles = self.create_member_roles(member)
        with self.bot.create_storage_session() as session:
            session.add(member_roles)
            session.commit()

    def clear_guild(self, guild):
        with self.bot.create_storage_session() as session:
            session.query(models.MemberRoles).filter(
                model.MemberRoles.guild_id==guild.id).delete()

    def create_member_roles(self, member):
        role_ids = list(member._roles)
        role_ids.remove(member.guild.default_role.id)
        return models.MemberRoles(
            guild_id=member.guild.id,
            user_id=member.id,
            role_ids=role_ids)
