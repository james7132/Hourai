import asyncio
import discord
import re
import typing
from datetime import datetime, timedelta
from discord.ext import commands
from hourai import utils
from hourai.db import proto
from hourai.cogs import BaseCog

MAX_PRUNE_LOOKBACK = timedelta(days=14)
DELETE_WAIT_DURATION = 60


async def batch_do(members, func):
    async def _do(member):
        result = ':thumbsup:'
        try:
            await func(member)
        except discord.Forbidden:
            result = "Bot has insufficient permissions."
        except Exception as e:
            result = str(e)
        return f"{member.name}: {result}"
    results = await asyncio.gather(*[_do(member) for member in members])
    return dict(zip(members, results))


class Admin(BaseCog):

    # --------------------------------------------------------------------------
    # General Admin commands
    # --------------------------------------------------------------------------

    async def _admin_action(self, ctx, members, func):
        results = await batch_do(members, func)
        await ctx.send(f"Executed command: `{ctx.message.clean_content}`\n"
                       + utils.format.vertical_list(results.values()),
                       delete_after=DELETE_WAIT_DURATION)

    @commands.command(name="kick")
    @commands.guild_only()
    @commands.has_permissions(kick_members=True)
    @commands.bot_has_permissions(kick_members=True)
    async def kick(self, ctx, *members: discord.Member):
        """Kicks all specified users."""
        await self._admin_action(ctx, members, lambda m: m.kick())

    @commands.command(name="ban")
    @commands.guild_only()
    @commands.has_permissions(ban_members=True)
    @commands.bot_has_permissions(ban_members=True)
    async def ban(self, ctx, *members: typing.Union[discord.Member, int]):
        """Bans all specified users from the server.
        Can be used with user IDs to ban users outside the server.
        """
        def _to_user(member):
            if isinstance(member, int):
                return utils.FakeSnowfake(id=member)
            return member
        members = (_to_user(mem) for mem in members)
        await self._admin_action(ctx, members, lambda m: m.ban())

    @commands.command(name="softban")
    @commands.guild_only()
    @commands.has_permissions(kick_members=True)
    @commands.bot_has_permissions(ban_members=True)
    async def softban(self, ctx, *members: discord.Member):
        """Bans then unbans all specified users from the server.
        Deletes the last 30 days of messages from the softbanned users.
        """
        async def _softban(member):
            await member.ban()
            await member.guild.unban(member)
        await self._admin_action(ctx, members, _softban)

    @commands.command(name="mute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def mute(self, ctx, *members: discord.Member):
        """Mutes all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(mute=True))

    @commands.command(name="unmute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def unmute(self, ctx, *members: discord.Member):
        """Unmutes all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(mute=False))

    @commands.command(name="deafen")
    @commands.guild_only()
    @commands.has_permissions(deafen_members=True)
    @commands.bot_has_permissions(deafen_members=True)
    async def deafen(self, ctx, *members: discord.Member):
        """Deafen all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(deafen=True))

    @commands.command(name="undeafen")
    @commands.guild_only()
    @commands.has_permissions(deafen_members=True)
    @commands.bot_has_permissions(deafen_members=True)
    async def undeafen(self, ctx, *members: discord.Member):
        """Deafen all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(deafen=False))

    @commands.command(name="move")
    @commands.guild_only()
    @commands.has_permissions(move_members=True)
    @commands.bot_has_permissions(move_members=True)
    async def move(self, ctx,
                   src: discord.VoiceChannel,
                   dst: discord.VoiceChannel):
        """Moves all members in one voice channel to another."""
        await self._admin_action(ctx, src.members,
                                 lambda m: m.edit(voice_channel=dst))

    @commands.command(name="nickname")
    @commands.guild_only()
    @commands.has_permissions(move_members=True)
    @commands.bot_has_permissions(move_members=True)
    async def nickname(self, ctx, name: str, *members: discord.Member):
        """Nicknames all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(nick=name))

    # -------------------------------------------------------------------------
    # Role commands
    # -------------------------------------------------------------------------

    @commands.group(name="role")
    async def role(self, ctx):
        """A group of roles for managing roles."""
        pass

    @role.command(name="add")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_add(self, ctx, role: discord.Role,
                       *members: discord.Member):
        """Adds a role to member."""
        await self._admin_action(ctx, members, lambda m: m.add_roles(role))

    @role.command(name="remove")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_remove(self, ctx, role: discord.Role,
                          *members: discord.Member):
        """Removes a role to member."""
        await self._admin_action(ctx, members, lambda m: m.remove_roles(role))

    @role.command(name="allow")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_allow(self, ctx, *, roles: discord.Role):
        """Allows one or more role to be self served."""

        async def _check_highest_role(member):
            highest_role = max(member.roles)
            for role in roles:
                if role > highest_role:
                    msg = (f'Cannot allow self serve of "{role.name}". Role is'
                           f' higher than {member.mention}\'s highest role.')
                    await ctx.send(msg, delete_after=DELETE_WAIT_DURATION)
                    return True
            return False

        if ((await _check_highest_role(ctx.guild.me)) or
           (await _check_highest_role(ctx.author))):
            return

        role_config = await ctx.bot.storage.role_configs.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        for role in roles:
            if role.id not in role_ids:
                role_config.role_ids.add(role.id)
        await ctx.bot.storage.set(ctx.guild.id, role_config)
        await ctx.send(f':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="forbid")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_forbid(self, ctx, *, roles: discord.Role):
        """Disallows one or more role to be self served."""

        async def _check_highest_role(member):
            highest_role = max(member.roles)
            for role in roles:
                if role > highest_role:
                    msg = (f'Cannot disallow self serve of "{role.name}". Role'
                           f' is higher than {member.mention}\'s highest '
                           f'role.')
                    await ctx.send(msg, delete_after=DELETE_WAIT_DURATION)
                    return True
            return False

        if ((await _check_highest_role(ctx.guild.me)) or
           (await _check_highest_role(ctx.author))):
            return

        role_config = await ctx.bot.storage.role_configs.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        for role in roles:
            if role.id in role_ids:
                role_config.role_ids.remove(role.id)
        await ctx.bot.storage.set(ctx.guild.id, role_config)
        await ctx.send(f':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="get")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_get(self, ctx, *, roles: discord.Role):
        """Adds a self-serve role to the caller."""
        role_config = await ctx.bot.storage.role_configs.get(ctx.guild.id)
        role_config = role_config or proto.RoleConfig()
        role_ids = set(role_config.self_serve_role_ids)
        # Ensure the roles can be safely added.
        roles = [r for r in roles if r < max(ctx.guild.me.roles)]
        for role in roles:
            if role.id not in role_ids:
                await ctx.send(f'`{role.name}` is not set up for self-serve.',
                               delete_after=DELETE_WAIT_DURATION)
                return
        reason = 'Self serve role(s) requested by user.'
        await ctx.author.add_roles(*roles, reasons=reason)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="drop")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_drop(self, ctx, *, roles: discord.Role):
        """Removes a self role from the caller."""
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
        await ctx.author.remove_roles(*roles, reasons=reason)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    # -------------------------------------------------------------------------
    # Temp commands
    # -------------------------------------------------------------------------

    @commands.group(name="temp")
    async def temp(self, ctx):
        pass

    @temp.group(name="ban")
    @commands.guild_only()
    @commands.has_permissions(ban_members=True)
    @commands.bot_has_permissions(ban_members=True)
    async def temp_ban(self, ctx, duration, *members: discord.Member):
        await self.ban(ctx, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    @temp.group(name="mute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def temp_mute(self, ctx, duration, *members: discord.Member):
        await self.mute(ctx, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    @temp.group(name="deafen")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def temp_deafen(self, ctx, duration, *members: discord.Member):
        await self.mute(ctx, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    @temp.group(name="role")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def temp_role(self, ctx, duration, *members: discord.Member):
        await self.ban(ctx, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    @temp_role.command(name="add")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def temp_role_add(self, ctx, duration, role: discord.Role,
                            *members: discord.Member):
        await self.role_add(ctx, role, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    @temp_role.command(name="role")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def temp_role_remove(self, ctx, duration, role: discord.Role,
                               *members: discord.Member):
        await self.role_remove(ctx, role, *members)
        # TODO(james7132): Implement this
        raise NotImplementedError

    # -------------------------------------------------------------------------
    # Prune commands
    # -------------------------------------------------------------------------

    async def _prune_messages(self, ctx, count, filter_func=None):
        async def _batcher():
            batch = []
            max_lookback = datetime.utcnow() - MAX_PRUNE_LOOKBACK
            async for msg in ctx.history(limit=count):
                if ((msg.created_at >= max_lookback) and
                        (filter_func is None or filter_func(msg))):
                    batch.append(msg)
                if len(batch) >= 100:
                    yield list(batch)
                    batch.clear()
            if len(batch) > 0:
                yield list(batch)

        tasks = []
        count = 0
        async for batch in _batcher():
            tasks.append(ctx.channel.delete_messages(batch))
            count += len(batch)
        await asyncio.gather(*tasks)
        return count

    @commands.group(name="prune")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_message=True)
    async def prune(self, ctx, count: int = 100):
        """Deletes the most recent messages in the current channel.
        Up to [count] messages will be deleted. By default this is 100.
        Messages over 14 days old will not be deleted.
        """
        count = await self._prune_messages(ctx, count)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="user")
    async def prune_user(self, ctx, *members: discord.Member):
        """Deletes all messages in the last 100 messages that belong to the
        specified users.  Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """
        members = set(members)
        count = await self._prune_messages(ctx, 100,
                                           lambda m: m.author in members)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="embed")
    async def prune_embed(self, ctx, *members: discord.Member):
        """Deletes all messages in the last 100 messages that have an embed or
        attachment.  Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """

        def msg_filter(m):
            return len(m.attachments) + len(m.embeds) > 0
        count = await self._prune_messages(ctx, 100, msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="bot")
    async def prune_bot(self, ctx, *members: discord.Member):
        """Deletes all messages in the last 100 messages from a bot.
        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """
        count = await self._prune_messages(ctx, 100,
                                           lambda m: m.author.bot)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="mine")
    async def prune_mine(self, ctx):
        """Deletes all messages in the last 100 messages written by the command
        caller.

        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """
        count = await self._prune_messages(ctx, 100,
                                           lambda m: m.author == ctx.author)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="mention")
    async def prune_mention(self, ctx):
        """Deletes all messages in the last 100 messages that mention another
        user or role.

        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """

        def msg_filter(m):
            return len(m.mentions) + len(m.role_mentions) > 0
        count = await self._prune_messages(ctx, 100, msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="regex")
    async def prune_match(self, ctx, regex: str):
        """Deletes all messages in the last 100 messages that match a certain
        pattern in the content.

        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        """
        regex = re.compile(regex)

        def msg_filter(m):
            return regex.search(m.clean_content)
        count = await self._prune_messages(ctx, 100, msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)


def setup(bot):
    pass
    # bot.add_cog(Admin())
