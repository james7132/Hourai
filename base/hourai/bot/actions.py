import discord
import asyncio
import typing
from datetime import datetime, timedelta
from hourai import utils
from hourai.utils import fake, format
from hourai.db import models, proto, escalation_history


def _get_reason(action: proto.Action) -> str:
    if action.HasField('reason'):
        return action.reason
    return None


# TODO(james7132): Log the actions to the log and to modlogs
class ActionManager:

    def __init__(self, bot):
        self.executor = ActionExecutor(bot)
        self.scheduler = ActionScheduler(bot)
        self._handlers = (self.executor, self.scheduler)

    def __getattr__(self, attr):
        for handler in self._handlers:
            try:
                return getattr(handler, attr)
            except AttributeError:
                pass
        raise AttributeError


class ActionScheduler:
    """Schedules pending actions into the future."""

    def __init__(self, bot):
        self.bot = bot

    def schedule(self, timestamp: int,
                *actions: typing.Iterable[proto.Action]) -> None:
        """Schedules an action to be done in the future."""
        session = self.bot.create_storage_session()
        with session:
            for action in actions:
                session.add(models.PendingAction(timestamp=timestamp,
                                                 data=action))
            session.commit()

    def query_pending_actions(self, session):
        """Queries a SQLAlchemy session for of the currently unexecuted pending
        actions.
        """
        now = datetime.utcnow()
        return session.query(models.PendingAction) \
            .filter(models.PendingAction.timestamp < now) \
            .order_by(models.PendingAction.timestamp) \
            .all()


class ActionExecutor:

    def __init__(self, bot):
        self.bot = bot

    async def execute_all(self, actions: typing.Iterable[proto.Action]) -> None:
        """Executes multiple actions in parallel."""
        return await asyncio.gather(*[self.execute(a) for a in actions])

    async def execute(self, action: proto.Action) -> None:
        action_type = action.WhichOneof('details')
        try:
            await getattr(self, "_apply_" + action_type)(action)
            if not action.HasField('duration'):
                return
            # Schedule an undo
            duration = timedelta(seconds=action.duration)
            self.bot.action_manager.schedule(datetime.utcnow() + duration,
                                             invert_action(action))
        except AttributeError:
            raise ValueError(f'Action type not supported: {action_type}')
        except discord.NotFound:
            # If the guild or the target is not found, silence the error
            pass
        except discord.Forbidden:
            # TODO(james7132): Properly report missing permissions
            # If the guild or the target is not found, silence the error
            pass
            self.bot.logger.exception('Error while executing action:')
        except Exception:
            self.bot.logger.exception('Error while executing action:')

    async def _apply_kick(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'kick'
        member = await self.__get_member(action)
        if member is not None:
            await member.kick(reason=_get_reason(action))

    async def _apply_ban(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'ban'
        guild = self.__get_guild(action)
        if guild is None:
            return
        assert action.HasField('user_id')
        user = fake.FakeSnowflake(id=action.user_id)
        if action.ban.type != proto.BanMember.UNBAN:
            await guild.ban(user,
                    reason=_get_reason(action),
                    delete_message_days=action.ban.delete_message_days)
        if action.ban.type != proto.BanMember.BAN:
            await guild.unban(user, reason=_get_reason(action))

    async def _apply_mute(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'mute'
        member = await self.__get_member(action)
        if member is not None:
            mute = {
                proto.StatusType.APPLY: True,
                proto.StatusType.UNAPPLY: False,
                # TODO(james7132): Implement this properly
                proto.MuteMember.TOGGLE: False,
            }[action.mute.type]
            await member.edit(mute=mute, reason=_get_reason(action))

    async def _apply_deafen(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'deafen'
        member = await self.__get_member(action)
        if member is not None:
            deafen = {
                proto.StatusType.APPLY: True,
                proto.StatusType.UNAPPLY: False,
                # TODO(james7132): Implement this properly
                proto.StatusType.TOGGLE: False,
            }[action.deafen.type]
            await member.edit(deafen=deafen, reason=_get_reason(action))

    async def _apply_change_role(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'change_role'
        member = await self.__get_member(action)
        if member is None:
            return
        roles = (member.guild.get_role(id)
                 for id in action.change_role.role_ids)
        roles = [r for r in roles if r is not None]
        if action.change_role.type == proto.StatusType.APPLY:
            await member.add_roles(*roles, reason=_get_reason(action))
        elif action.change_role.type == proto.StatusyType.UNAPPLY:
            await member.remove_roles(*roles, reason=_get_reason(action))
        elif action.change_role.type == proto.StatusType.TOGGLE:
            role_ids = set(member._roles)
            add_roles = [r for r in roles if r.id not in roles_ids]
            rm_roles = [r for r in roles if r.id in roles_ids]
            await asyncio.gather(
                member.add_roles(*add_roles, reason=_get_reason(action)),
                member.remove(*rm_roles, reason=_get_reason(action))
            )

    async def _apply_escalate(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'escalate'
        guild = self.__get_guild(action)
        if guild is None:
            return
        history = escalation_history.UserEscalationHistory(
                self.bot, fake.FakeSnowflake(id=action.user_id), guild)
        # TODO(james7132): Log this
        await history.apply_diff(guild.me, action.reason,
                                 action.escalate.amount)

    async def _apply_direct_message(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'direct_message'
        user = await self.__get_user(action)
        if user is None or not action.direct_message.content:
            return
        try:
            content = format.ellipsize(action.direct_message.content)
            await user.send(content=action.direct_message.content)
        except (discord.Forbidden, discord.NotFound):
            # Don't cause a ruckus if the user has the bot blocked
            pass

    async def _apply_command(self, action: proto.Action) -> None:
        assert action.WhichOneof('details') == 'command'
        channel = self.bot.get_channel(action.command.channel_id)
        try:
            guild = channel.guild
        except AttributeError:
            guild = self.bot.get_guild(action.guild_id)

        if action.HasField('user_id'):
            user = (await self.__get_member(action)) if guild is not None \
                    else (await self.__get_user(action))
        else:
            user = (guild.me if guild is not None else self.bot.user)

        ctx = await self.bot.get_automated_context(
            content=action.command.command, author=user,
            channel=channel, guild=guild)
        async with ctx:
            await self.bot.invoke(ctx)

    def __get_guild(self, action: proto.Action) -> discord.Guild:
        assert action.HasField('guild_id')
        return self.bot.get_guild(action.guild_id)

    def __get_user(self, action: proto.Action) -> discord.User:
        assert action.HasField('user_id')
        return utils.get_user_async(self.bot, action.user_id)

    def __get_member(self, action: proto.Action) -> discord.Member:
        assert action.HasField('user_id')
        # FIXME: This will not work once the bot is larger than a single process.
        guild = self.__get_guild(action)
        if guild is None:
            return None
        return utils.get_member_async(guild, action.user_id)


def _invert_ban(self, action: proto.Action) -> proto.Action:
    action.ban.type = {
        proto.BanMember.BAN: proto.BanMember.UNBAN,
        proto.BanMember.UNBAN: proto.BanMember.BAN,
    }.get(action.ban.type, action.ban.type)


def _invert_status(attr: str):
    def invert_status_action(action: proto.Action) -> proto.Action:
        sub_proto = getattr(action, attr)
        sub_proto.type = {
            proto.StatusType.APPLY: proto.StatusType.UNAPPLY,
            proto.StatusType.UNAPPLY: proto.StatusType.APPLY
        }.get(sub_proto.type, sub_proto.type)
    return invert_status_action


def _invert_escalate(action: proto.Action) -> proto.Action:
    action.escalate.amount *= -1


INVERT_MAPPING = {
    "ban": _invert_ban,
    "change_role": _invert_status('change_role'),
    "mute": _invert_status('mute'),
    "deafen": _invert_status('deafen'),
    "escalate": _invert_escalate,
}


def invert_action(action: proto.Action) -> proto.Action:
    new_action = proto.Action()
    new_action.CopyFrom(action)

    if action.HasField('reason'):
        new_action.reason = 'Undo: ' + action.reason
    new_action.ClearField('duration')

    try:
        INVERT_MAPPING[action.WhichOneOf('details')](new_action)
    except KeyError:
        raise ValueError('Provided action cannot be inverted.')

    return new_action
