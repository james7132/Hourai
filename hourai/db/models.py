import collections
import discord
import enum
import inspect
import sys
from .proto import action_pb2
from .proto import auto_config_pb2
from .proto import escalation_pb2
from .proto import event_pb2
from .proto import guild_configs_pb2
from datetime import datetime, timezone
from abc import abstractmethod
from sqlalchemy import types
from sqlalchemy import Column, UniqueConstraint
from sqlalchemy.schema import Table, ForeignKey
from sqlalchemy.orm import relationship
from sqlalchemy.ext.declarative import declarative_base

FakeUser = collections.namedtuple('FakeUser', 'id')

Base = declarative_base()

feed_channels_table = Table('feed_channels', Base.metadata,
                            Column('feed_id', types.BigInteger,
                                   ForeignKey('feeds.id')),
                            Column('channel_id', types.BigInteger,
                                   ForeignKey('channels.id')))

class UnixTimestamp(types.TypeDecorator):
    impl = types.BigInteger

    def __init__(self):
        types.TypeDecorator.__init__(self)

    def process_bind_param(self, value, dialect):
        return int(value.replace(tzinfo=timezone.utc).timestamp() * 1000)

    def process_result_value(self, value, dialect):
        return datetime.utcfromtimestamp(value / 1000)

class Protobuf(types.TypeDecorator):
    impl = types.JSON

    def __init__(self, message_type):
        self.message_type = message_type
        types.TypeDecorator.__init__(self)

    def process_bind_param(self, value, dialect):
        return int(value.replace(tzinfo=timezone.utc).timestamp() * 1000)

    def process_result_value(self, value, dialect):
        return datetime.utcfromtimestamp(value / 1000)

class Username(Base):
    __tablename__ = 'usernames'

    user_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    timestamp = Column(UnixTimestamp, primary_key=True)
    name = Column(types.String(255), nullable=False)
    discriminator = Column(types.Integer)

    def to_fullname(self):
        return (self.name
                if self.discriminator is None
                else f'{self.name}#{self.discriminator}')

    @classmethod
    def from_resource(cls, resource, *args, **kwargs):
        kwargs.update({
            'id': resource.id,
            'username': resource.name,
            'timestamp': datetime.utcnow(),
        })
        return cls(*args, **kwargs)


class Guild(Base):
    __tablename__ = 'guilds'

    id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    logging = Column(Protobuf(guild_configs_pb2.LoggingConfig),
                     nullable=True)
    validation = Column(Protobuf(guild_configs_pb2.ValidationConfig),
                        nullable=True)
    auto = Column(Protobuf(auto_config_pb2.AutoConfig),
                  nullable=True)
    moderation = Column(Protobuf(guild_configs_pb2.ModerationConfig),
                        nullable=True)

class LoggingConfig(Base):
    __tablename__ = 'guild_logging_configs'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    modlog_channel_id = Column(types.BigInteger)
    log_deleted_messages = Column(types.Boolean, nullable=False)
    log_edited_messages = Column(types.Boolean, nullable=False)

    @property
    def is_valid(self):
        components = (self.validation_role_id, self.validation_channel_id)
        return all(x is not None for x in components)

class GuildValidationConfig(Base):
    __tablename__ = 'guild_validation_configs'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    validation_role_id = Column(types.BigInteger, nullable=False)
    validation_channel_id = Column(types.BigInteger, nullable=False)
    is_propogated = Column(types.Boolean, nullable=False, default=False)

    @property
    def is_valid(self):
        components = (self.validation_role_id, self.validation_channel_id)
        return all(x is not None for x in components)


class Alias(Base):
    __tablename__ = 'aliases'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    name = Column(types.String(2000), primary_key=True)
    content = Column(types.String(2000))


class Channel(Base):
    __tablename__ = 'channels'

    id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    guild_id = Column(types.BigInteger, nullable=True)

    feeds = relationship("Feed", secondary=feed_channels_table,
                         back_populates="channels")

    def get_resource(self, bot):
        return bot.get_channel(self.id)

@enum.unique
class FeedType(enum.Enum):
    RSS = enum.auto()
    REDDIT = enum.auto()
    HACKER_NEWS = enum.auto()
    TWITTER = enum.auto()

class Feed(Base):
    __tablename__ = 'feeds'
    __table_args__ = (UniqueConstraint('type', 'source'),)

    id = Column(types.Integer, primary_key=True, autoincrement=True)
    _type = Column('type', types.String(255), nullable=False)
    source = Column(types.String(8192), nullable=False)
    last_updated = Column(UnixTimestamp, nullable=False)

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


# class ActionSource(Base):
    # __tablename__ = 'action_source'

    # id = Column(types.Integer, primary_key=True)
    # authorizer_id = Column(types.BigInteger, nullable=False)
    # timestamp = Column(UnixTimestamp, nullable=False)

    # message_id = Column(types.BigInteger)
    # message_content = Column(types.String(2000))


# class Action(Base):
    # __tablename__ = 'actions'

    # id = Column(types.Integer, primary_key=True)
    # type = Column(types.String(255), nullable=False)
    # state = Column(types.Enum(ActionState), nullable=False)
    # guild_id = Column(types.BigInteger)
    # user_id = Column(types.BigInteger)
    # channel_id = Column(types.BigInteger)
    # role_id = Column(types.BigInteger)
    # start_timestamp = Column(UnixTimestamp)
    # end_timestamp = Column(UnixTimestamp)
    # action_metadata = Column(types.LargeBinary)

    # __mapper_args__ = {
        # 'polymorphic_on': type,
        # 'polymorphic_identity': 'action'
    # }

    # async def execute(self, bot):
        # if self.state != ActionState.PENDING:
            # raise RuntimeError('Cannot run an already finished action.')
        # try:
            # await self.apply(bot)
            # self.state = ActionState.SUCCESS
        # except:
            # self.state = ActionState.FAILURE

    # async def apply(self, bot):
        # raise NotImplementedError

    # def create_undo(self):
        # cls_name = inspect.getmembers(self.__class__)['undo_action_type']
        # cls = getattr(sys.modules[__name__], cls_name)
        # copy = self.copy(cls)
        # copy.start_timestamp = datetime.utcnow()
        # copy.end_timestamp = None
        # return copy

    # def copy(self, cls):
        # return cls(guild_id=self.guild_id,
                   # user_id=self.user_id,
                   # channel_id=self.channel_id,
                   # role_id=self.role_id,
                   # metadata=self.metadata)

    # def get_guild(self, bot):
        # return bot.get_guild(self.guild_id)

    # def get_user(self, bot):
        # return bot.get_user(self.user_id)

    # def get_member(self, bot):
        # return self.get_guild().get_member(self.user_id)

    # def get_role(self, bot):
        # return bot.get_channel(self.channel_id)

    # def get_role(self, bot):
        # return self.get_guild().get_role(role_id)


# class BanAction(Action):

    # __mapper_args__ = {'polymorphic_identity': 'ban'}
    # undo_action_type = 'UnbanAction'

    # async def apply(self, bot):
        # await self.get_guild().ban(FakeUser(id=user_id))


# class UnbanAction(Action):

    # __mapper_args__ = {'polymorphic_identity': 'unban'}
    # undo_action_type = 'BanAction'

    # async def apply(self, bot):
        # await self.get_guild().unban(FakeUser(id=user_id))


# class KickAction(Action):

    # __mapper_args__ = {'polymorphic_identity': 'kick'}

    # async def apply(self, bot):
        # await self.get_member().kick()

    # def create_undo(self):
        # raise NotImplementedError('Kicking cannot be undone!')


# class AddRoleAction(Action):

    # __mapper_args__ = {'polymorphic_identity': 'add_role'}
    # undo_action_type = 'RemoveRoleAction'

    # async def apply(self, bot):
        # await self.get_member().add_roles(self.get_role())


# class RemoveRoleAction(Action):

    # __mapper_args__ = {'polymorphic_identity': 'remove_role'}
    # undo_action_type = 'AddRoleAction'

    # async def apply(self, bot):
        # await self.get_member().remove_roles(self.get_role())
