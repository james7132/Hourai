import discord
from discord.ext import commands
from hourai import utils
from hourai.bot import cogs
from hourai.db import models, proto


class RoleLogging(cogs.BaseCog):
    """ Cog for logging role changes. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_member_join(self, member):
        if not member.pending:
            await self.on_join(member)

    @commands.Cog.listener()
    async def on_member_update(self, before, after):
        if before.pending and not after.pending:
            await self.on_join(after)

    async def on_join(self, member: discord.Member):
        await self.restore_roles(member)

    async def restore_roles(self, member: discord.Member):
        roles = set()
        guild = member.guild
        config = guild.config.role
        with self.bot.create_storage_session() as session:
            member_roles = session.query(models.MemberRoles).get(
                    (guild.id, member.id))
            if member_roles is None:
                return
            for role_id in member_roles.role_ids:
                settings = config.settings.get(role_id)
                role = guild.get_role(role_id)
                if role is None or settings is None:
                    continue
                if proto.RoleFlags(settings.flags).restorable and \
                   utils.can_manage_role(guild.me, role):
                    roles.add(role)

        # Exclude the validation role if validation is enabled.
        if guild.config.validation.enabled:
            roles.discard(guild.validation_role)

        if len(roles) > 0:
            await member.add_roles(
                    *roles, reason="Restoring roles upon rejoining.")
            self.bot.logger.info(
                f'Restored roles for member {member.id} in guild {guild.id}')
