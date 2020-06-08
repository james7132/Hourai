import asyncio
from datetime import datetime, timedelta
from hourai.utils import fake, format
from hourai.db import models, proto, escalation_history


def _get_reason(action):
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

    def schedule(self, timestamp, *actions):
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

    async def execute_all(self, actions):
        """Executes multiple actions in parallel."""
        return await asyncio.gather(*[self.execute(a) for a in actions])

    async def execute(self, action):
        action_type = action.WhichOneof('details')
        try:
            handler = getattr(self, "_apply_" + action_type)
        except AttributeError:
            raise ValueError(f'Action type not supported: {action_type}')
        try:
            await handler(action)
            if not action.HasField('duration'):
                return
            # Schedule an undo
            duration = timedelta(seconds=action.duration)
            self.bot.action_manager.schedule(
                datetime.utcnow() + duration,
                ActionExecutor.__invert_action(action))
        except Exception:
            self.bot.logger.exception('Error while executing action:')

    @staticmethod
    def __invert_action(action):
        inverted = invert_action(action)
        return inverted

    async def _apply_kick(self, action):
        assert action.WhichOneof('details') == 'kick'
        member = self.__get_member(action)
        if member is not None:
            await member.kick(reason=_get_reason(action))

    async def _apply_ban(self, action):
        assert action.WhichOneof('details') == 'ban'
        guild = self.__get_guild(action)
        if guild is None:
            return
        assert action.HasField('user_id')
        user = fake.FakeSnowflake(id=action.user_id)
        if action.ban.type != proto.BanMember.UNBAN:
            await guild.ban(user, reason=_get_reason(action))
        if action.ban.type != proto.BanMember.BAN:
            await guild.unban(user, reason=_get_reason(action))

    async def _apply_mute(self, action):
        assert action.WhichOneof('details') == 'mute'
        member = self.__get_member(action)
        if member is not None:
            mute = {
                proto.MuteMenber.MUTE: True,
                proto.MuteMenber.UNMUTE: False,
                # TODO(james7132): Implement this properly
                proto.MuteMember.TOGGLE: False,
            }[action.mute.type]
            await member.edit(mute=mute, reason=_get_reason(action))

    async def _apply_deafen(self, action):
        assert action.WhichOneof('details') == 'deafen'
        member = self.__get_member(action)
        if member is not None:
            deafen = {
                proto.DeafenMember.DEAFEN: True,
                proto.DeafenMember.UNDEAFEN: False,
                # TODO(james7132): Implement this properly
                proto.DeafenMember.TOGGLE: False,
            }[action.deafen.type]
            await member.edit(deafen=deafen, reason=_get_reason(action))

    async def _apply_change_role(self, action):
        assert action.WhichOneof('details') == 'change_role'
        member = self.__get_member(action)
        if member is None:
            return
        roles = (member.guild.get_role(id)
                 for id in action.change_role.role_ids)
        roles = [r for r in roles if r is not None]
        if action.change_role.type == proto.ChangeRole.ADD:
            await member.add_roles(*roles, reason=_get_reason(action))
        elif action.change_role.type == proto.ChangeRole.REMOVE:
            await member.remove_roles(*roles, reason=_get_reason(action))
        elif action.change_role.type == proto.ChangeRole.TOGGLE:
            role_ids = set(member._roles)
            add_roles = [r for r in roles if r.id not in roles_ids]
            rm_roles = [r for r in roles if r.id in roles_ids]
            await asyncio.gather(
                member.add_roles(*add_roles, reason=_get_reason(action)),
                member.remove(*rm_roles, reason=_get_reason(action))
            )

    async def _apply_escalate(self, action):
        assert action.WhichOneof('details') == 'escalate'
        guild = self.__get_guild(action)
        if guild is None:
            return
        history = escalation_history.UserEscalationHistory(
                self.bot, fake.FakeSnowflake(id=action.user_id), guild)
        # TODO(james7132): Log this
        await history.apply_diff(guild.me, action.reason,
                                 action.escalate.amount)

    async def _apply_direct_message(self, action):
        assert action.WhichOneof('details') == 'direct_message'
        user = self.__get_user(action)
        if user is None:
            return
        try:
            content = format.ellipsize(action.direct_message.content)
            await user.send(content=action.direct_message.content)
        except (discord.Forbidden, discord.NotFound):
            # Don't cause a ruckus if the user has the bot blocked
            pass

    async def _apply_command(self, action):
        assert action.WhichOneof('details') == 'command'
        channel = self.bot.get_channel(action.command.channel_id)
        try:
            guild = channel.guild
        except AttributeError:
            guild = self.bot.get_guild(action.guild_id)
        if action.HasField('user_id'):
            user = (guild.get_member(action.user_id)
                    if guild is not None else
                    self.bot.get_user(action.user_id))
        else:
            user = (guild.me if guild is not None else self.bot.user)
        ctx = await self.bot.get_automated_context(
            content=action.command.command, author=user,
            channel=channel, guild=guild)
        async with ctx:
            await self.bot.invoke(ctx)

    def __get_guild(self, action):
        assert action.HasField('guild_id')
        return self.bot.get_guild(action.guild_id)

    def __get_user(self, action):
        assert action.HasField('user_id')
        return self.bot.get_user(action.user_id)

    def __get_member(self, action):
        assert action.HasField('user_id')
        guild = self.__get_guild(action)
        if guild is None:
            return None
        return guild.get_member(action.user_id)


def invert_action(action):
    new_action = proto.Action()
    new_action.CopyFrom(action)
    case = new_action.WhichOneof('details')
    if case == 'ban':
        new_action.ban.type = {
            proto.BanMember.BAN: proto.BanMember.UNBAN,
            proto.BanMember.UNBAN: proto.BanMember.BAN,
        }.get(new_action.ban.type, new_action.ban.type)
    elif case == 'mute':
        new_action.mute.type = {
            proto.MuteMenber.MUTE: proto.MuteMember.UNMUTE,
            proto.MuteMenber.UNMUTE: proto.MuteMember.MUTE
        }.get(action.mute.type, action.mute.type)
    elif case == 'deafen':
        new_action.deafen.type = {
            proto.DeafenMember.DEAFEN: proto.DeafenMember.UNDEAFEN,
            proto.DeafenMember.UNDEAFEN: proto.DeafenMember.DEAFEN
        }.get(action.deafen.type, action.deafen.type)
    elif case == 'change_role':
        new_action.change_role.type = {
            proto.ChangeRole.ADD: proto.ChangeRole.REMOVE,
            proto.ChangeRole.REMOVE: proto.ChangeRole.ADD
        }.get(action.change_role.type, action.change_role.type)
    elif case == 'escalate':
        new_action.escalate.amount *= -1
    else:
        raise ValueError('Provided action cannot be inverted.')
    if action.HasField('reason'):
        new_action.reason = 'Undo: ' + action.reason
    new_action.ClearField('duration')
    return new_action
