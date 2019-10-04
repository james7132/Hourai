import asyncio
from datetime import datetime, timedelta
from hourai.utils import fake
from hourai.db import models, proto


def _get_reason(action):
    if action.HasField('reason'):
        return action.reason
    return None


class ActionExecutor:

    def __init__(self, bot):
        self.bot = bot
        self._handlers = {
            'kick': self.__apply_kick,
            'ban': self.__apply_ban,
            'mute': self.__apply_mute,
            'deafen': self.__apply_deafen,
            'change_role': self.__apply_change_role,
        }

    async def execute_actions(self, actions):
        return await asyncio.gather(*[self.execute_action(a) for a in actions])

    async def execute_action(self, action):
        action_type = action.WhichOneof('details')
        handler = self._handlers.get(action_type)
        if handler is not None:
            try:
                await handler(action)
                if not action.HasField('duration'):
                    return
                duration = timedelta(seconds=action.duration)
                timestamp = datetime.utcnow() + duration
                inverted = ActionExecutor.__invert_action(action)

                session = self.bot.create_storage_session()
                with session:
                    session.add(models.PendingAction(timestamp=timestamp,
                                                    data=inverted))
                    session.commit()
            except Exception:
                self.bot.logger.exception('Error while executing action:')
        else:
            raise ValueError(f'Action type not supported: {action_type}')

    @staticmethod
    def __invert_action(action):
        inverted = invert_action(action)
        if action.HasField('reason'):
            inverted.reason = 'Undo: ' + action.reason
        inverted.ClearField('duration')
        return inverted

    async def __apply_kick(self, action):
        assert action.WhichOneof('details') == 'kick'
        member = self.__get_member(action)
        if member is not None:
            await member.kick(reason=_get_reason(action))

    async def __apply_ban(self, action):
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

    async def __apply_mute(self, action):
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

    async def __apply_deafen(self, action):
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

    async def __apply_change_role(self, action):
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
        elif action.change_role.type == proto.ChangeRole.REMOVE:
            add_roles = [r for r in roles if r not in member.roles]
            rm_roles = [r for r in roles if r in member.roles]
            await asyncio.gather(
                member.add_roles(*add_roles, reason=_get_reason(action)),
                member.remove(*rm_roles, reason=_get_reason(action))
            )

    def __get_guild(self, action):
        assert action.HasField('guild_id')
        return self.bot.get_guild(action.guild_id)

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
            proto.BanMember.BAN:  proto.BanMember.UNBAN,
            proto.BanMember.UNBAN:  proto.BanMember.BAN,
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
    else:
        raise ValueError('Provided action cannot be inverted.')
    return new_action
