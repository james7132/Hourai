import enum
from .proto import auto_config_pb2
from .proto import guild_configs_pb2
from datetime import datetime, timezone
from sqlalchemy import types
from sqlalchemy import Column, UniqueConstraint
from sqlalchemy.schema import Table, ForeignKey, Index
from sqlalchemy.orm import relationship
from sqlalchemy.ext.declarative import declarative_base

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


Index("idx_username_user_id", Username.user_id)


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
