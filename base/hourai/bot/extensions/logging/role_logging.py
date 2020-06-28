import logging
import discord
import asyncio
from sqlalchemy import func
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
    async def on_member_update(self, before, after):
        self.log_member_roles(after)

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        self.log_member_roles(member)

    @commands.Cog.listener()
    async def on_guild_join(self, guild):
        await self.log_guild_roles(guild)

    @commands.Cog.listener()
    async def on_guild_remove(self, guild):
        self.clear_guild(guild)

    @commands.Cog.listener()
    async def on_guild_role_delete(self, role):
        self.clear_role(role)

    async def log_all_guilds(self):
        # FIXME: This will not scale to multiple processes/machines.
        await asyncio.gather(*[self.log_guild_roles(guild)
                               for guild in self.bot.guilds])

    async def log_guild_roles(self, guild):
        model = models.MemberRoles
        members = guild.fetch_members(limit=None)
        async for chunk in iterable.chunked_async(members, chunk_size=1000):
            roles = {member.id: self.create_member_roles(member)
                     for member in chunk}
            with self.bot.create_storage_session() as session:
                existing = session.query(model) \
                                  .filter_by(guild_id=guild.id) \
                                  .filter(model.user_id.in_(roles.keys())) \
                                  .all()

                for existing_roles in existing:
                    # Only merge those posts which already exist in the database
                    new_roles = roles.pop(existing_roles.user_id)
                    new_roles = session.merge(new_roles)
                    if len(new_roles.role_ids) <= 0:
                        session.delete(new_roles)

                # Only add those posts which did not exist in the database
                session.add_all(roles.values())
                session.commit()

    def log_member_roles(self, member):
        member_roles = self.create_member_roles(member)
        with self.bot.create_storage_session() as session:
            id = (member.guild.id, member.id)
            existing = session.query(models.MemberRoles).get(id)

            if existing:
                if len(member_roles.role_ids) <= 0:
                    session.delete(existing)
                else:
                    member_roles = session.merge(member_roles)
            else:
                session.add(member_roles)

            session.commit()

    def clear_role(self, role):
        assert isinstance(role, discord.Role)
        with self.bot.create_storage_session() as session:
            session.execute(f"""
            UPDATE member_roles
            SET role_ids = array_remove(role_ids, {role.id})
            WHERE guild_id = {role.guild.id}
            """)
            session.execute(f"""
            DELETE FROM member_roles
            WHERE guild_id = {role.guild.id} AND cardinality(role_ids) = 0
            """)

    def clear_guild(self, guild):
        with self.bot.create_storage_session() as session:
            session.query(models.MemberRoles).filter_by(guild_id=guild.id) \
                   .delete()

    def create_member_roles(self, member):
        role_ids = list(member._roles)
        if member.guild.default_role.id in role_ids:
            role_ids.remove(member.guild.default_role.id)
        return models.MemberRoles(
            guild_id=member.guild.id,
            user_id=member.id,
            role_ids=role_ids)
