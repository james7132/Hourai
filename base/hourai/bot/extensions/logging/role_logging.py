import discord
from discord.ext import commands
from hourai.bot import cogs
from hourai.db import models, proto
from hourai.utils import iterable


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
        self.log_member_roles(member)

    async def restore_roles(self, member: discord.Member):
        roles = list()
        proxy = self.bot.get_guild_proxy(member.guild)
        config = proxy.config.get('role')
        with self.bot.create_storage_session() as session:
            member_roles = session.query(models.MemberRoles).get(
                    (member.guild.id, member.id))
            if member_roles is None:
                return
            for role_id in member_role.role_ids:
                settings = config.settings.get(role_id)
                role = member.guild.get_role(role_id)
                if role is None or settings is None:
                    continue
                if proto.RoleFlags(settings.flags).restorable and \
                   utils.can_manage_role(member.guild.me, role):
                    roles.add(role)

        # Exclude the validation role if validation is enabled.
        validation_config = proxy.config.get('validation')
        if validation_config.enabled:
            roles.discard(member.guild.get_role(validation_config.role_id))

        if len(roles) > 0:
            await member.add_roles(
                    *roles, reason="Restoring roles upon rejoining.")
            self.bot.logger.info(
                f'Restored roles for member {member.id} in guild '
                f'{member.guild.id}')

    @commands.Cog.listener()
    async def on_raw_member_update(self, data):
        if data['user'].get('bot'):
            return
        guild_id = data['guild_id']
        user = data['user']
        role_ids = [int(id) for id in data['roles']]
        self.log_roles(guild_id, user['id'], role_ids)

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
            roles = {member.id: self.create_roles(member)
                     for member in chunk if not member.bot}
            with self.bot.create_storage_session() as session:
                existing = session.query(model) \
                                  .filter_by(guild_id=guild.id) \
                                  .filter(model.user_id.in_(roles.keys())) \
                                  .all()

                for existing_roles in existing:
                    # Only merge those posts which already exist in the
                    # database
                    session.merge(roles.pop(existing_roles.user_id))

                # Only add those posts which did not exist in the database
                # with roles
                roles = {key: value for key, value in roles.items()
                         if len(value.role_ids) > 0}
                session.add_all(roles.values())
                session.commit()
                self._clear_empty(session)

    def log_member_roles(self, member):
        if member.bot:
            return
        self.log_roles(member.guild.id, member.id, member._roles)

    def log_roles(self, guild_id, member_id, role_ids):
        member_roles = self.create_member_roles(guild_id, member_id, role_ids)
        with self.bot.create_storage_session() as session:
            id = (guild_id, member_id)
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
            f'Updated roles for user {member_id}, guild {guild_id}')

    def clear_role(self, role):
        assert isinstance(role, discord.Role)
        with self.bot.create_storage_session() as session:
            session.execute(f"""
            UPDATE member_roles
            SET role_ids = array_remove(role_ids, {role.id})
            WHERE guild_id = {role.guild.id}
            """)
            self._clear_empty(session)

    def create_roles(self, member):
        return self.create_member_roles(
                member.guild.id, member.id, member._roles)

    def create_member_roles(self, guild_id, member_id, role_ids):
        return models.MemberRoles(guild_id=guild_id, user_id=member_id,
                                  role_ids=role_ids)

    def _clear_empty(self, session):
        session.execute(
            "DELETE FROM member_roles WHERE cardinality(role_ids) = 0")
