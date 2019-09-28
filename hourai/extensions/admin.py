import asyncio
import discord
import re
import typing
from datetime import datetime, timedelta
from discord.ext import commands
from hourai import bot, utils

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


class Admin(bot.BaseCog):

    # --------------------------------------------------------------------------
    # General Admin Commands
    # --------------------------------------------------------------------------

    async def _admin_action(self, ctx, members, func):
        results = await batch_do(members, func)
        await ctx.send(f"Executed command: `{ctx.message.clean_content}`\n"
                       + utils.format.vertical_list(results.values()),
                       delete_after=DELETE_WAIT_DURATION)

    @commands.Command(name="kick")
    @commands.guild_only()
    @commands.has_permissions(kick_members=True)
    @commands.bot_has_permissions(kick_members=True)
    async def kick(self, ctx, *members: discord.Member):
        """Kicks all specified users."""
        await self._admin_action(ctx, members, lambda m: m.kick())

    @commands.Command(name="ban")
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

    @commands.Command(name="softban")
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

    @commands.Command(name="mute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def mute(self, ctx, *members: discord.Member):
        """Mutes all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(mute=True))

    @commands.Command(name="unmute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def unmute(self, ctx, *members: discord.Member):
        """Unmutes all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(mute=False))

    @commands.Command(name="deafen")
    @commands.guild_only()
    @commands.has_permissions(deafen_members=True)
    @commands.bot_has_permissions(deafen_members=True)
    async def deafen(self, ctx, *members: discord.Member):
        """Deafen all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(deafen=True))

    @commands.Command(name="undeafen")
    @commands.guild_only()
    @commands.has_permissions(deafen_members=True)
    @commands.bot_has_permissions(deafen_members=True)
    async def undeafen(self, ctx, *members: discord.Member):
        """Deafen all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(deafen=False))

    @commands.Command(name="move")
    @commands.guild_only()
    @commands.has_permissions(move_members=True)
    @commands.bot_has_permissions(move_members=True)
    async def move(self, ctx,
                   src: discord.VoiceChannel,
                   dst: discord.VoiceChannel):
        """Moves all members in one voice channel to another."""
        await self._admin_action(ctx, src.members,
                                 lambda m: m.edit(voice_channel=dst))

    @commands.Command(name="nickname")
    @commands.guild_only()
    @commands.has_permissions(move_members=True)
    @commands.bot_has_permissions(move_members=True)
    async def nickname(self, ctx, name: str, *members: discord.Members):
        """Nicknames all specified users."""
        await self._admin_action(ctx, members, lambda m: m.edit(nick=name))

    # -------------------------------------------------------------------------
    # Role Commands
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
    @commands.bot_has_permissions(manage_roles=True)
    async def role_allow(self, ctx, role: discord.Role):
        """Allows a role to be self served."""
        raise NotImplementedError

    @role.command(name="get")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_get(self, ctx, role: discord.Role):
        """Adds a self-serve role to the caller."""
        # TODO(james7132): Implement this
        if True:
            await ctx.send(f'`{role.name}` is not set up for self-serve.',
                           delete_after=DELETE_WAIT_DURATION)
            return
        if role not in ctx.author.roles:
            await ctx.author.add_roles(role)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="drop")
    @commands.guild_only()
    @commands.bot_has_permissions(manage_roles=True)
    async def role_drop(self, ctx, role: discord.Role,
                        *members: discord.Member):
        """Removes a self role from the caller."""
        # TODO(james7132): Implement this
        if True:
            await ctx.send(f'`{role.name}` is not set up for self-serve.',
                           delete_after=DELETE_WAIT_DURATION)
            return
        if role not in ctx.author.roles:
            await ctx.author.add_roles(role)
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    # -------------------------------------------------------------------------
    # Temp Commands
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
    # Prune Commands
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
