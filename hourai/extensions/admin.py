import asyncio
import discord
import re
import typing
import pytimeparse
import logging
from datetime import datetime, timedelta
from discord.ext import commands, tasks
from hourai import utils
from hourai.db import proto, models
from hourai.cogs import BaseCog

MAX_PRUNE_LOOKBACK = timedelta(days=14)
DELETE_WAIT_DURATION = 60

log = logging.getLogger(__name__)


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


def human_timedelta(time_str):
    seconds = pytimeparse.parse(time_str)
    if seconds is None or not isinstance(seconds, int):
        raise ValueError
    return timedelta(seconds=seconds)


def create_action(member):
    return proto.Action(user_id=member.id, guild_id=member.guild.id)


class Admin(BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.apply_pending_actions.start()

    def cog_unload(self):
        self.apply_pending_actions.cancel()

    @tasks.loop(seconds=1)
    async def apply_pending_actions(self):
        try:
            session = self.bot.create_storage_session()
            with session:
                now = datetime.utcnow()
                query = session.query(models.PendingAction) \
                    .filter(models.PendingAction.timestamp < now) \
                    .order_by(models.PendingAction.timestamp) \
                    .all()
                for pending_action in query:
                    await self.bot.action_executor.execute_action(
                        pending_action.data)
                    session.delete(pending_action)
                    session.commit()
        except Exception:
            log.exception('Error in running pending action:')

    @apply_pending_actions.before_loop
    async def before_apply_pending_actions(self):
        await self.bot.wait_until_ready()

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

    @role.command(name="list")
    async def role_list(self, ctx):
        """Lists all of the roles on the server."""
        await ctx.send(format.code_list(r.name for r in ctx.guild.roles))

    @role.command(name="add")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_add(self, ctx, role: discord.Role,
                       *members: discord.Member):
        """Adds a role to server members."""
        def make_action(member):
            action = create_action(member)
            action.change_role.type = proto.ChangeRole.ADD
            action.change_role.role_ids.append(role.id)
            action.reason = (f'Role added by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @role.command(name="remove")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def role_remove(self, ctx, role: discord.Role,
                          *members: discord.Member):
        """Removes a role from server members."""
        def make_action(member):
            action = create_action(member)
            action.change_role.type = proto.ChangeRole.REMOVE
            action.change_role.role_ids.append(role.id)
            action.duration = int(duration.total_seconds())
            action.reason = (f'Role removed by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

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
    async def temp_ban(self, ctx, duration: human_timedelta,
                       *members: discord.Member):
        def make_action(member):
            action = create_action(member)
            action.ban.type = proto.BanMember.BAN
            action.duration = int(duration.total_seconds())
            action.reason = (f'Temp Ban by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @temp.group(name="mute")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def temp_mute(self, ctx, duration: human_timedelta,
                        *members: discord.Member):
        def make_action(member):
            action = create_action(member)
            action.mute.type = proto.MuteMember.MUTE
            action.duration = int(duration.total_seconds())
            action.reason = (f'Temp mute by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)

    @temp.group(name="deafen")
    @commands.guild_only()
    @commands.has_permissions(mute_members=True)
    @commands.bot_has_permissions(mute_members=True)
    async def temp_deafen(self, ctx, duration: human_timedelta,
                          *members: discord.Member):
        def make_action(member):
            action = create_action(member)
            action.deafen.type = proto.DeafenMember.DEAFEN
            action.duration = int(duration.total_seconds())
            action.reason = (f'Temp deafen by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @temp.group(name="role")
    async def temp_role(self, ctx):
        pass

    @temp_role.command(name="add")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def temp_role_add(self, ctx, duration: human_timedelta,
                            role: discord.Role, *members: discord.Member):
        def make_action(member):
            action = create_action(member)
            action.change_role.type = proto.ChangeRole.ADD
            action.change_role.role_ids.append(role.id)
            action.duration = int(duration.total_seconds())
            action.reason = (f'Temp role by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    @temp_role.command(name="role")
    @commands.guild_only()
    @commands.has_permissions(manage_roles=True)
    @commands.bot_has_permissions(manage_roles=True)
    async def temp_role_remove(self, ctx, duration: human_timedelta,
                               role: discord.Role, *members: discord.Member):
        def make_action(member):
            action = create_action(member)
            action.change_role.type = proto.ChangeRole.REMOVE
            action.change_role.role_ids.append(role.id)
            action.duration = int(duration.total_seconds())
            action.reason = (f'Temp role by {ctx.author.name}.\n' +
                             ctx.message.jump_url)
            return action
        await ctx.bot.action_executor.execute_actions(
            make_action(m) for m in members)
        # TODO(james7132): Have this reflect the results of the actions
        await ctx.send(':thumbsup:', delete_after=DELETE_WAIT_DURATION)

    # -------------------------------------------------------------------------
    # Prune commands
    # -------------------------------------------------------------------------

    async def _prune(self, ctx, count=100, predicate=None):
        predicate = predicate or (lambda m: True)
        max_lookback = datetime.utcnow() - MAX_PRUNE_LOOKBACK

        def _msg_filter(msg):
            return msg != ctx.message and predicate(msg)

        async def _batcher():
            batch = []
            seen = 0
            async for msg in ctx.history():
                seen += 1
                if seen > count or msg.created_at < max_lookback:
                    break
                if msg == ctx.message or not predicate(msg):
                    continue
                batch.append(msg)
                if len(batch) >= 100:
                    yield list(batch)
                    batch.clear()
            if len(batch) > 0:
                yield list(batch)

        seen_messages = 0
        async for batch in _batcher():
            assert len(batch) > 0
            await ctx.channel.delete_messages(batch)
            seen_messages += len(batch)
        return seen_messages

    @commands.group(name="prune", invoke_without_command=True)
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune(self, ctx, count: int = 100):
        """Prunes messages in the current channel.

        Up to [count] messages will be deleted. By default this is 100.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """
        count = await self._prune(ctx, count=count)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="user")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_user(self, ctx, *members: discord.Member):
        """Prunes messages in the current channel that belong to specific users.

        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """
        members = set(members)

        def msg_filter(m):
            return m.author in members
        count = await self._prune(ctx, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="embed")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_embed(self, ctx, count: int = 100):
        """Prunes messages in the current channel that have an embed.

        Up to [count] messages will be deleted. Defaults to 100.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """

        def msg_filter(m):
            return len(m.attachments) + len(m.embeds) > 0
        count = await self._prune(ctx, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="bot")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_bot(self, ctx, count: int = 100):
        """Prunes messages in the current channel sent by bots.

        Up to [count] messages will be deleted. Defaults to 100.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """
        def msg_filter(m):
            return m.author.bot
        count = await self._prune(ctx, count=count, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="mine")
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_mine(self, ctx, count: int = 100):
        """Prunes messages in the current channel from the caller.

        Up to [count] messages will be deleted. Defaults to 100.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (Bot)
        """
        def msg_filter(m):
            return m.author == ctx.author
        count = await self._prune(ctx, count=count, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="mention")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_mention(self, ctx, count: int = 100):
        """Prunes messages in the current channel that mentions anyone.

        Up to [count] messages will be deleted. Defaults to 100.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """
        def msg_filter(m):
            return len(m.mentions) + len(m.role_mentions) > 0 or \
                    m.mention_everyone
        count = await self._prune(ctx, count=count, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)

    @prune.command(name="match")
    @commands.has_permissions(manage_messages=True)
    @commands.bot_has_permissions(manage_messages=True)
    async def prune_match(self, ctx, regex: str):
        """Prunes messages in the current channel that matches a pattern.

        Example: ~prune match hi matches "hi", "high", "hiiiii".

        Up to 100 messages will be deleted.
        Messages over 14 days old will not be deleted.
        Requires: Manage Messages (User and Bot)
        """
        regex = re.compile(regex)

        def msg_filter(m):
            return regex.search(m.clean_content)
        count = await self._prune(ctx, predicate=msg_filter)
        await ctx.send(f":thumbsup: Deleted {count} messages.",
                       delete_after=DELETE_WAIT_DURATION)


def setup(bot):
    bot.add_cog(Admin(bot))
