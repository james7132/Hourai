import copy
import hourai.utils as utils
import typing
import discord
from discord import Member, Role
from discord.ext import commands
from discord.ext.commands.cooldowns import BucketType
from hourai import bot, db
from hourai.db import models

DAYS = 24 * 60 * 60


def _get_moderated_user(member):
    moderated_user = db.moderated_user.get(member)
    if moderated_user is not None:
        return moderated_user

    moderated_user = ModeratedUser()
    moderated_user.member_id.CopyFrom(_get_id_proto(member))

    return moderated_user


def clamp(a, smallest, largest):
    return max(smallest, min(a, largest))


class Admin(bot.BaseCog):
    pass

    # @commands.command()
    # @commands.guild_only()
    # async def escalate(self, ctx, reason: str, *members: discord.Member):
        # ladder = _get_admin_config(ctx.guild).ladder
        # ladder_size = len(ladder.rung)
        # if ladder_size <= 0:
            # await ctx.send('Server has no configured escalation ladder. Cannot escalate')
        # actions = []
        # for member in members:
            # moderated_user = _get_moderated_user(member)
            # index = moderated_user.current_level
            # index = clamp(index, 0, ladder_size -1)
            # rung = ladder.rung[index]
            # actions.append((member, moderated_user, rung))
        # source = action_util.create_action_source(ctx)
        # for member, model, rung in actions:
            # action = copy.deep_copy(rung.action)
            # action.source.CopyFrom(source)
            # action.context.reason = reason

        # pass

    # @commands.group()
    # async def config(self, ctx):
        # pass

    # @config.command()
    # @commands.guild_only()
    # @commands.is_owner()
    # async def set(self, ctx, *, config: str):
        # raise NotImplementedError

    # @config.command(name='dump')
    # @commands.guild_only()
    # @commands.is_owner()
    # async def config_dump(self, ctx):
        # raise NotImplementedError

    # @commands.command()
    # @commands.guild_only()
    # @commands.has_permissions(kick_members=True)
    # @commands.bot_has_permissions(kick_members=True)
    # @commands.cooldown(5, 5, BucketType.guild)
    # @bot.action_command
    # async def kick(self, ctx, *members: Member):
        # """Kicks all provided users from the server."""
        # action = action_util.create_action(ctx)
        # for member in members:
            # id_proto = action_util.id_to_proto(member)
            # action.kick_member.member_id.CopyFrom(id_proto)
            # yield copy.deepcopy(action)

    # @commands.command()
    # @commands.guild_only()
    # @commands.has_permissions(ban_members=True)
    # @commands.bot_has_permissions(ban_members=True)
    # @commands.cooldown(5, 5, BucketType.guild)
    # @bot.action_command
    # async def ban(self, ctx, *members: typing.Union[int, Member]):
        # """Bans all provided users from the server."""
        # action = action_util.create_action(ctx)
        # for member in members:
            # id_proto = _get_id_proto(member)
            # action.ban_member.member_id.CopyFrom(id_proto)
            # yield copy.deepcopy(action)

    # # Role commands

    # @commands.group()
    # async def role(self, ctx):
        # """A set of commands to change the roles of users."""
        # pass

    # @role.command(name="add")
    # @commands.guild_only()
    # @commands.has_permissions(manage_roles=True)
    # @commands.bot_has_permissions(manage_roles=True)
    # @commands.cooldown(5, 5, BucketType.guild)
    # @bot.action_command
    # async def role_add(self, ctx, role: Role, *members: Member):
        # """Adds a role to all listed users."""
        # action = action_util.create_action(ctx)
        # action.change_role.role_id.append(role.id)
        # action.change_role.type = ChangeRole.ADD
        # for member in members:
            # id_proto = _get_id_proto(member)
            # action.change_role.member_id.CopyFrom(id_proto)
            # yield copy.deepcopy(action)

    # @role.command(name="remove")
    # @commands.guild_only()
    # @commands.has_permissions(manage_roles=True)
    # @commands.bot_has_permissions(manage_roles=True)
    # @commands.cooldown(5, 5, BucketType.user)
    # @bot.action_command
    # async def role_remove(self, ctx, role: Role, *members: Member):
        # """Removes a role to all listed users."""
        # action = action_util.create_action(ctx)
        # action.change_role.role_id.append(role.id)
        # action.change_role.type = ChangeRole.REMOVE
        # for member in members:
            # id_proto = _get_id_proto(member)
            # action.change_role.member_id.CopyFrom(id_proto)
            # yield copy.deepcopy(action)

    # @role.command(name="get")
    # @commands.guild_only()
    # @commands.bot_has_permissions(manage_roles=True)
    # @commands.cooldown(5, 5, BucketType.user)
    # async def role_get(self, ctx, role: Role, *embers: Member):
        # """Adds a self-serve role to the caller.."""
        # pass

    # @role.command(name="drop")
    # @commands.guild_only()
    # @commands.bot_has_permissions(manage_roles=True)
    # @commands.cooldown(5, 5, BucketType.user)
    # async def role_drop(self, ctx, role: Role, *embers: Member):
        # """Removes a self-serve role to the caller.."""
        # pass

    # @role.command(name="list")
    # @commands.guild_only()
    # @commands.cooldown(5, 5, BucketType.user)
    # async def role_list(self, ctx, role: Role, *embers: Member):
        # """Lists all of the roles on the server."""
        # await ctx.say(", ".joiin(f"`{role.name}`" for role in ctx.guild.roles))


def setup(bot):
    pass
    # bot.add_cog(Admin())
