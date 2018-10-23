import queue
import asyncio
import hourai.data.actions_pb2
import logging
from collections import named_tuple
from hourai.data.actions_pb2 import ActionStatus
from google.protobuf import text_format

log = logging.getLogger(__name__)

FakeUser = namedtuple('FakeUser', ['id'])


ACTION_MAP = {
    'send_messsage': SendMessage,
    'kick_member': KickMember,
    'ban_member': BanMember,
    'change_member_role': ChangeRole,
}

def create(action_proto):
    action_type = action_proto.WhichOneOf('action')
    return ACTION_MAP[action_type](action_proto)


def _get_guild(bot, proto):
    guild = bot.get_guild(proto.guild_id)
    if guild is None:
        raise LookupError(f'Could not find guild: {proto.guild_id}.')
    return guild


def _get_member(bot, proto):
    guild = _get_guild(bot, proto)
    member = guild.get_member(proto.member_id)
    if guild is None:
        raise LookupError(f'Could not find guild: {proto.guild_id}.')
    return guild


class Action():

    @staticmethod
    def from_proto(action_proto):
        action_type = action_proto.WhichOneof('action')
        if action_type not in ACTION_MAP:
            raise ValueError(f'Unsupported action type:  {action_type}')
        return ACTION_MAP[action_type](action_proto)

    @abstractmethod
    async def commit(self, bot):
        pass

    async def revert(self, bot):
        # By default actions cannot be undone
        pass

    def create_undo(self):
        return InverseAction(self)


class InverseAction(Action):

    def __init__(self, original):
        self.original = original

    async def commit(self, bot):
        await self.original.revert(bot)

    async def revert(self, bot):
        await self.original.commit(bot)


class SendMessage(Action):

    async def commit(self, bot):
        message = self.proto.message
        channel = await self._get_channel(bot, message)
        if channel is None:
            raise LookupError(
                f'Could not find a Discord channel send message to.')
        msg = await channel.send(message.content)
        log.info(
            f'[SendMessage] Sent message {msg.id} in {msg.channel.id}: {msg.content}')

    async def _get_channel(self, bot, message):
        channel = None
        if message.HasField('channel_id'):
            channel = channel or bot.get_channel(message.channel_id)
        if message.HasField('user_id'):
            channel = channel or await self._get_dm_channel(bot, message)
        return channel

    async def _get_dm_channel(self, bot, message):
        user = bot.get_user(message.user_id)
        if user is None:
            return None
        return user.dm_channel or await user.create_dm()


class KickMember(Action):

    async def commit(self, bot):
        guild = _get_guild(self.proto.member_id)
        # TODO(james7132): Add reason to this
        user = FakeUser(id=self.proto.member_id.user_id)
        await guild.kick(user)
        log.info(
            f'[KickMember] Kicked user {user.id} from guild: {guild.name} ({guild.id}.')


class BanMember(Action):

    async def commit(self, bot):
        guild = _get_guild(self.proto.member_id)
        # TODO(james7132): Add reason to this
        user = FakeUser(id=self.proto.member_id.user_id)
        await guild.ban(user, delete_message_days=0)
        log.info(
            f'Banned user {user.id} from guild: {guild.name} ({guild.id}.')

    async def revert(self, bot):
        guild = _get_guild(self.proto.member_id)
        # TODO(james7132): Add reason to this
        user = FakeUser(id=self.proto.member_id.user_id)
        await guild.unban(user)
        log.info(f'Unbanned user {user.id} from guild: {guild.name} ({guild.id}.')


class ChangeRole(Action):

    async def commit(self, bot):
        guild = _get_guild(self.proto.member_id)
        # TODO(james7132): Add reason to this
        user = FakeUser(id=self.proto.member_id.user_id)
        await guild.ban(user, delete_message_days=0)
        log.info(
            f'[BanMember] Banned user {user.id} from guild: {guild.name} ({guild.id}.')

    async def revert(self, bot):
        guild = _get_guild(self.proto.member_id)
        # TODO(james7132): Add reason to this
        user = FakeUser(id=self.proto.member_id.user_id)
        await guild.unban(user)
        log.info(
            f'[BanMember] Banned user {user.id} from guild: {guild.name} ({guild.id}.')
