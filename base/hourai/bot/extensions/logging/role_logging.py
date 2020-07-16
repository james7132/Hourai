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
        if before._roles == after._roles:
            return
        self.log_member_roles(after)

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        self.log_member_roles(member)

    @commands.Cog.listener()
    async def on_guild_join(self, guild):
        await self.log_guild_roles(guild)

    @commands.Cog.listener()
    async def on_guild_role_delete(self, role):
        self.clear_role(role)

    async def log_all_guilds(self):
        # FIXME: This will not scale to multiple processes/machines.
        for guild in self.bot.guilds:
            await self.log_guild_roles(guild)

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
                    session.merge(roles.pop(existing_roles.user_id))

                # Only add those posts which did not exist in the database
                # with roles
                roles = {key: value for key, value in roles.items()
                         if len(value.role_ids) > 0}
                session.add_all(roles.values())
                session.commit()
                self._clear_empty(session)

    def log_member_roles(self, member):
        member_roles = self.create_member_roles(member)
        with self.bot.create_storage_session() as session:
            id = (member.guild.id, member.id)
            existing = session.query(models.MemberRoles).get(id)

            if len(member_roles.role_ids) <= 0:
                if existing:
                    session.delete(existing)
                else:
                    return
            else:
                if existing:
                    session.merge(member_roles)
                else:
                    session.add(member_roles)

            session.commit()

        self.bot.logger.info(
            f'Updated roles for user {member.id}, guild {member.guild.id}')

    def clear_role(self, role):
        assert isinstance(role, discord.Role)
        with self.bot.create_storage_session() as session:
            session.execute(f"""
            UPDATE member_roles
            SET role_ids = array_remove(role_ids, {role.id})
            WHERE guild_id = {role.guild.id}
            """)
            self._clear_empty(session)

    def create_member_roles(self, member):
        return models.MemberRoles(
            guild_id=member.guild.id,
            user_id=member.id,
            role_ids=member._roles)

    def _clear_empty(self, session):
        session.execute(
            f"DELETE FROM member_roles WHERE cardinality(role_ids) = 0")
