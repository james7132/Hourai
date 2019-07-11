import collections
import discord
import enum
import inspect
import sys
from datetime import datetime
from abc import abstractmethod
from sqlalchemy import Column, LargeBinary, BigInteger, Boolean, DateTime, Enum, String, UniqueConstraint
from sqlalchemy.schema import Table, ForeignKey
from sqlalchemy.orm import relationship
from sqlalchemy.ext.declarative import declarative_base, declared_attr

FakeUser = collections.namedtuple('FakeUser', 'id')

Base = declarative_base()

feed_channels_table = Table('feed_channels', Base.metadata,
                            Column('feed_id', BigInteger,
                                   ForeignKey('feeds.id')),
                            Column('channel_id', BigInteger,
                                   ForeignKey('channels.id')))


@enum.unique
class FeedType(enum.Enum):
    RSS = enum.auto()
    REDDIT = enum.auto()
    HACKER_NEWS = enum.auto()
    TWITTER = enum.auto()


@enum.unique
class ActionState(enum.Enum):
    PENDING = enum.auto()
    SUCCESS = enum.auto()
    FAILURE = enum.auto()



class Username(Base):
    __tablename__ = 'usernames'

    user_id = Column(BigInteger, primary_key=True, autoincrement=False)
    username = Column(String(255), primary_key=True)
    timestamp = Column(DateTime, nullable=False)

    @classmethod
    def from_resource(cls, resource, *args, **kwargs):
        kwargs.update({
            'id': resource.id,
            'username': resource.name,
            'timestamp': datetime.utcnow(),
        })
        return cls(*args, **kwargs)


class GuildValidationConfig(Base):
    __tablename__ = 'guild_validation_configs'

    guild_id = Column(BigInteger, primary_key=True, autoincrement=False)
    validation_role_id = Column(BigInteger, nullable=False)
    validation_channel_id = Column(BigInteger, nullable=False)


class CustomCommand(Base):
    __tablename__ = 'commands'

    guild_id = Column(BigInteger, primary_key=True, autoincrement=False)
    name = Column(String(2000), primary_key=True)
    content = Column(String(2000))


class Channel(Base):
    __tablename__ = 'channels'

    id = Column(BigInteger, primary_key=True, autoincrement=False)
    guild_id = Column(BigInteger, nullable=True)

    feeds = relationship("Feed", secondary=feed_channels_table,
                         back_populates="channels")


class Feed(Base):
    __tablename__ = 'feeds'
    __table_args__ = (UniqueConstraint('type', 'source'),)

    id = Column(BigInteger, primary_key=True)
    _type = Column('type', String(255), nullable=False)
    source = Column(String(8192), nullable=False)

    channels = relationship("Channel", secondary=feed_channels_table,
                            back_populates="feeds")

    @property
    def type(self):
        return FeedType._member_map_.get(self._type, None)

    @type.setter
    def set_type(self, value):
        assert isinstance(value, FeedType)
        self._type = value.name

    def get_channels(self, bot):
        """ Returns a generator for all channels in the feed. """
        return (ch.get_resource(bot) for ch in self.channels)


class ActionSource(Base):
    __tablename__ = 'action_source'

    id = Column(BigInteger, primary_key=True)
    authorizer_id = Column(BigInteger, nullable=False)
    timestamp = Column(DateTime, nullable=False)

    message_id = Column(BigInteger)
    message_content = Column(String(2000))


class Action(Base):
    __tablename__ = 'actions'

    id = Column(BigInteger, primary_key=True)
    type = Column(String(255), nullable=False)
    state = Column(Enum(ActionState), nullable=False)
    guild_id = Column(BigInteger)
    user_id = Column(BigInteger)
    channel_id = Column(BigInteger)
    role_id = Column(BigInteger)
    start_timestamp = Column(DateTime)
    end_timestamp = Column(DateTime)
    action_metadata = Column(LargeBinary)

    __mapper_args__ = {
        'polymorphic_on': type,
        'polymorphic_identity': 'action'
    }

    async def execute(self, bot):
        if self.state != ActionState.PENDING:
            raise RuntimeError('Cannot run an already finished action.')
        try:
            await self.apply(bot)
            self.state = ActionState.SUCCESS
        except:
            self.state = ActionState.FAILURE

    async def apply(self, bot):
        raise NotImplementedError

    def create_undo(self):
        cls_name = inspect.getmembers(self.__class__)['undo_action_type']
        cls = getattr(sys.modules[__name__], cls_name)
        copy = self.copy(cls)
        copy.start_timestamp = datetime.utcnow()
        copy.end_timestamp = None
        return copy

    def copy(self, cls):
        return cls(guild_id=self.guild_id,
                   user_id=self.user_id,
                   channel_id=self.channel_id,
                   role_id=self.role_id,
                   metadata=self.metadata)

    def get_guild(self, bot):
        return bot.get_guild(self.guild_id)

    def get_user(self, bot):
        return bot.get_user(self.user_id)

    def get_member(self, bot):
        return self.get_guild().get_member(self.user_id)

    def get_role(self, bot):
        return bot.get_channel(self.channel_id)

    def get_role(self, bot):
        return self.get_guild().get_role(role_id)


class BanAction(Action):

    __mapper_args__ = {'polymorphic_identity': 'ban'}
    undo_action_type = 'UnbanAction'

    async def apply(self, bot):
        await self.get_guild().ban(FakeUser(id=user_id))


class UnbanAction(Action):

    __mapper_args__ = {'polymorphic_identity': 'unban'}
    undo_action_type = 'BanAction'

    async def apply(self, bot):
        await self.get_guild().unban(FakeUser(id=user_id))


class KickAction(Action):

    __mapper_args__ = {'polymorphic_identity': 'kick'}

    async def apply(self, bot):
        await self.get_member().kick()

    def create_undo(self):
        raise NotImplementedError('Kicking cannot be undone!')


class AddRoleAction(Action):

    __mapper_args__ = {'polymorphic_identity': 'add_role'}
    undo_action_type = 'RemoveRoleAction'

    async def apply(self, bot):
        await self.get_member().add_roles(self.get_role())


class RemoveRoleAction(Action):

    __mapper_args__ = {'polymorphic_identity': 'remove_role'}
    undo_action_type = 'AddRoleAction'

    async def apply(self, bot):
        await self.get_member().remove_roles(self.get_role())
