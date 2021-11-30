import asyncio
import discord
import re
import typing
import logging
from . import escalation
from datetime import datetime, timedelta
from discord.ext import commands, tasks
from hourai import utils
from hourai.utils import fake, format
from hourai.db import proto
from hourai.bot import cogs


DELETE_WAIT_DURATION = 60

log = logging.getLogger(__name__)


async def check_role_manager(ctx, *targets):
    for target in targets:
        if not utils.can_manage_role(ctx.author, target):
            await ctx.send(
                f'{ctx.author.mention}, you are not allowed to manage '
                f'`{target.name}`.', delete_after=DELETE_WAIT_DURATION)
            return False

        if not utils.can_manage_role(ctx.guild.me, target):
            await ctx.send(
                f'{ctx.guild.me.mention} is not allowed to manage '
                f'`{target.name}`.', delete_after=DELETE_WAIT_DURATION)
            return False

    return True


async def deprecation_notice(ctx, alt):
    await ctx.send(
        f"This command is deprecated and will be removed soon. Please use the "
        f"`/{alt}` slash command instead. For more information on how to use "
        f"Hourai's Slash Commands, please read the documentation here: "
        f"https://docs.hourai.gg/Slash-Commands.")


class Admin(escalation.EscalationMixin, cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot

    # --------------------------------------------------------------------------
    # General Admin commands
    # --------------------------------------------------------------------------

    @commands.command(name="kick")
    async def kick(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "kick")

    @commands.command(name="ban")
    async def ban(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "ban")

    @commands.command(name="softban")
    async def softban(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "ban")

    @commands.command(name="mute")
    async def mute(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "mute")

    @commands.command(name="deafen")
    async def deafen(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "deafen")

    @commands.command(name="move")
    async def move(self, ctx, *, remainder: str = None):
        await deprecation_notice(ctx, "move")

    @commands.command(name="nickname")
    async def nickname(self, ctx,  *members: discord.Member):
        await deprecation_notice(ctx, "nickname")

    # -------------------------------------------------------------------------
    # Role commands
    # -------------------------------------------------------------------------

    @commands.group(name="role")
    async def role(self, ctx):
        """A group of commands for managing roles."""
        pass

    @role.command(name="list")
    async def role_list(self, ctx, *, remainder: str):
        """Lists all of the roles on the server."""
        await ctx.send(format.code_list(r.name for r in ctx.guild.roles))

    @role.command(name="add")
    async def role_add(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "role add")

    @role.command(name="remove")
    async def role_remove(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "role remove")

    @role.command(name="allow")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_allow(self, ctx, *roles: discord.Role):
        """Allows one or more role to be self served.

        Examples:
          ~role allow DotA
          ~role allow Gamer "League of Legends"

        Running this command allows normal users to use ~role get and
        ~role drop.
        Requires Manage Roles (User and Bot)
        """
        if not (await check_role_manager(ctx, *roles)):
            return

        storage = ctx.bot.storage.role_configs
        role_config = await storage.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        for role in roles:
            if role.id not in role_ids:
                role_config.self_serve_role_ids.append(role.id)
        await storage.set(ctx.guild.id, role_config)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="forbid")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_forbid(self, ctx, *roles: discord.Role):
        """Disallows one or more role to be self served.

        Examples:
          ~role forbid DotA
          ~role forbid Gamer "League of Legends"

        Running this command disallows normal users to use ~role get and
        ~role drop.
        Requires Manage Roles (User and Bot)
        """
        if not (await check_role_manager(ctx, *roles)):
            return

        storage = ctx.bot.storage.role_configs
        role_config = await storage.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        for role in roles:
            if role.id in role_ids:
                role_config.self_serve_role_ids.remove(role.id)
        await storage.set(ctx.guild.id, role_config)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="get")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_get(self, ctx, *roles: discord.Role):
        """Adds self-serve roles to the caller.

        Examples:
          ~role get DotA
          ~role get Gamer "League of Legends"

        Roles must be allowed via ~role allow before they can be used with this
        command. See ~help role allow for more information.
        Requires Manage Roles (Bot)
        """
        role_config = await ctx.bot.storage.role_configs.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        # Ensure the roles can be safely added.
        top_role = ctx.guild.me.top_role
        for role in roles:
            if role >= top_role:
                await ctx.send(f"`{role.name}` is higher than bot's highest.",
                               delete_after=DELETE_WAIT_DURATION)
                return
            if role.id not in role_ids:
                await ctx.send(f'`{role.name}` is not set up for self-serve.',
                               delete_after=DELETE_WAIT_DURATION)
                return
        reason = 'Self serve role(s) requested by user.'
        await ctx.author.add_roles(*roles, reason=reason)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="drop")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_drop(self, ctx, *roles: discord.Role):
        """Removes self-serve roles to the caller.

        Examples:
          ~role drop DotA
          ~role drop Gamer "League of Legends"

        Roles must be allowed via ~role allow before they can be used with this
        command. See ~help role allow for more information.
        Requires Manage Roles (Bot)
        """
        role_config = await ctx.bot.storage.role_configs.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        # Ensure the roles can be safely removed.
        roles = [r for r in roles if r < max(ctx.guild.me.roles)]
        for role in roles:
            if role.id not in role_ids:
                await ctx.send(f'`{role.name}` is not set up for self-serve.',
                               delete_after=DELETE_WAIT_DURATION)
                return
        reason = 'Self serve role(s) requested to be removed by user.'
        await ctx.author.remove_roles(*roles, reason=reason)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    # -------------------------------------------------------------------------
    # Temp commands
    # -------------------------------------------------------------------------

    @commands.group(name="temp")
    async def temp(self, ctx):
        """Group of commands for running temporary changes."""
        pass

    @temp.group(name="ban")
    async def temp_ban(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "")

    @temp.group(name="mute")
    async def temp_mute(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "mute")

    @temp.group(name="deafen")
    async def temp_deafen(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "deafen")

    @temp.group(name="role")
    async def temp_role(self, ctx, *, remainder: str):
        """Group of commands for temporarily altering roles."""
        pass

    @temp_role.command(name="add")
    async def temp_role_add(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "role add")

    @temp_role.command(name="remove")
    async def temp_role_remove(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "role remove")

    # -------------------------------------------------------------------------
    # Prune commands
    # -------------------------------------------------------------------------

    @commands.group(name="prune")
    async def prune(self, ctx, *, remainder: str):
        await deprecation_notice(ctx, "prune")

def setup(bot):
    bot.add_cog(Admin(bot))
