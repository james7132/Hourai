import copy
import hourai.util as util
import typing
import hourai.db as db
from discord import Member, Role
from discord.ext import commands
from discord.ext.commands.cooldowns import BucketType
from google.protobuf import text_format
from hourai.data import action_util
from hourai.data.admin_pb2 import *
from hourai.data.actions_pb2 import *
from hourai.data.models_pb2 import *

DAYS = 24 * 60 * 60


def _get_id_proto(member, ctx):
    if isinstance(member, int):
        guild_id = ctx.channel.guild.id
        return MemberId(user_id=id, guild_id=guild_id)
    elif isinstance(member, Member):
        return action_util.id_to_proto(member)
    return None


def _get_admin_config(guild):
    config = db.admin_configs.get(guild)
    if config is not None:
        return config

    config = AdminConfig(guild_id=guild.id)

    warn = config.ladder.rung.add()
    warn.deescalation_period.FromSeconds(90 * DAYS)
    warn.action.send_message.content = 'This is a formal warning from the mods. Please correct your behavior'
    warn.action.context.description = "Warning"

    kick = config.ladder.rung.add()
    kick.deescalation_period.FromSeconds(90 * DAYS)
    kick.action.kick_member.SetInParent()
    kick.action.context.description = "Kick"

    ban = config.ladder.rung.add()
    ban.action.ban_member.SetInParent()
    ban.action.context.description = "Ban"

    return config


def _get_moderated_user(member):
    moderated_user = db.moderated_user.get(member)
    if moderated_user is not None:
        return moderated_user

    moderated_user = ModeratedUser()
    moderated_user.member_id.CopyFrom(_get_id_proto(member))

    return moderated_user


def clamp(a, smallest, largest):
    return max(smallest, min(a, largest))


class Admin(util.BaseCog):

    def __init__(self, bot):
        super().__init__(bot)

    @commands.command()
    @commands.guild_only()
    async def escalate(self, ctx, reason: str, *members: discord.Member):
        ladder = _get_admin_config(ctx.guild).ladder
        ladder_size = len(ladder.rung)
        if ladder_size <= 0:
            await ctx.send('Server has no configured escalation ladder. Cannot escalate')
        actions = []
        for member in members:
            moderated_user = _get_moderated_user(member)
            index = moderated_user.current_level
            index = clamp(index, 0, ladder_size -1)
            rung = ladder.rung[index]
            actions.append((member, moderated_user, rung))
        source = action_util.create_action_source(ctx)
        for member, model, rung in actions:
            action = copy.deep_copy(rung.action)
            action.source.CopyFrom(source)
            action.context.reason = reason

        pass

    @commands.group()
    async def config(self, ctx):
        pass

    @config.command()
    @commands.guild_only()
    @commands.is_owner()
    async def set(self, ctx, *, config: str):
        with db.admin_configs.begin(write=True) as txn:
            admin = AdminConfig()
            text_format.Merge(config.strip(), admin)
            txn.put(ctx.guild, admin)
        await success()

    @config.command()
    @commands.guild_only()
    @commands.is_owner()
    async def dump(self, ctx):
        config = db.admin_configs.get(
            ctx.guild) or _default_admin_config(ctx.guild)
        text_proto = text_format.MessageToString(config, as_utf8=True)
        await ctx.send(f'```{text_proto}```')

    @commands.command()
    @commands.guild_only()
    @commands.has_permissions(kick_members=True)
    @commands.bot_has_permissions(kick_members=True)
    @commands.cooldown(5, 5, BucketType.guild)
    @util.action_command
    async def kick(self, ctx, *members: Member):
        """Kicks all provided users from the server."""
        action = action_util.create_action(ctx)
        for member in members:
            id_proto = action_util.id_to_proto(member)
            action.kick_member.member_id.CopyFrom(id_proto)
            yield copy.deepcopy(action)

    @commands.command()
    @commands.guild_only()
    @commands.has_permissions(ban_members=True)
    @commands.bot_has_permissions(ban_members=True)
    @commands.cooldown(5, 5, BucketType.guild)
    @util.action_command
    async def ban(self, ctx, *members: typing.Union[int, Member]):
        """Bans all provided users from the server."""
        action = action_util.create_action(ctx)
        for member in members:
            id_proto = _get_id_proto(member)
            action.ban_member.member_id.CopyFrom(id_proto)
            yield copy.deepcopy(action)

    # Role commands

    @commands.group()
    async def role(self, ctx):
        """A set of commands to change the roles of users."""
        pass

    @role.command(name="add")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    @commands.cooldown(5, 5, BucketType.user)
    @util.action_command
    async def role_add(self, ctx, role: Role, *members: Member):
        """Adds a role to all listed users."""
        action = action_util.create_action(ctx)
        action.change_role.role_id.append(role.id)
        action.change_role.type = ChangeRole.ADD
        for member in members:
            id_proto = _get_id_proto(member)
            action.change_role.member_id.CopyFrom(id_proto)
            yield copy.deepcopy(action)

    @role.command(name="remove")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    @commands.cooldown(5, 5, BucketType.user)
    @util.action_command
    async def role_remove(self, ctx, role: Role, *members: Member):
        """Removes a role to all listed users."""
        action = action_util.create_action(ctx)
        action.change_role.role_id.append(role.id)
        action.change_role.type = ChangeRole.REMOVE
        for member in members:
            id_proto = _get_id_proto(member)
            action.change_role.member_id.CopyFrom(id_proto)
            yield copy.deepcopy(action)

    @role.command(name="toggle")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    @commands.cooldown(5, 5, BucketType.user)
    @util.action_command
    async def role_toggle(self, ctx, role: Role, *members: Member):
        """Removes a role to all listed users."""
        action = action_util.create_action(ctx)
        action.change_role.role_id.append(role.id)
        action.change_role.type = ChangeRole.TOGGLE
        for member in members:
            id_proto = _get_id_proto(member)
            action.change_role.member_id.CopyFrom(id_proto)
            yield copy.deepcopy(action)

    @role.command(name="get")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    @commands.cooldown(5, 5, BucketType.user)
    async def role_get(self, ctx, role: Role, *embers: Member):
        """Adds a self-serve role to the caller.."""
        pass

    @role.command(name="drop")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    @commands.cooldown(5, 5, BucketType.user)
    async def role_get(self, ctx, role: Role, *embers: Member):
        """Removes a self-serve role to the caller.."""
        pass

    @role.command(name="list")
    @commands.guild_only()
    @commands.cooldown(5, 5, BucketType.user)
    async def role_get(self, ctx, role: Role, *embers: Member):
        """Lists all of the roles on the server."""
        await ctx.say(", ".joiin(f"`{role.name}`" for role in ctx.guild.roles))


def setup(bot):
    bot.add_cog(Admin(bot))
