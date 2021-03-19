import enum
from . import proto
from sqlalchemy import types
from sqlalchemy import Column, UniqueConstraint
from sqlalchemy.schema import Table, ForeignKey, Index
from sqlalchemy.orm import relationship
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.dialects import postgresql

Base = declarative_base()


class Protobuf(types.TypeDecorator):
    impl = types.LargeBinary

    def __init__(self, message_type):
        self.message_type = message_type
        types.TypeDecorator.__init__(self)

    def process_bind_param(self, value, dialect):
        assert isinstance(value, self.message_type), (
            f'{type(value)} cannot be assigned to a column of type '
            f'{self.message_type}')
        return value.SerializeToString()

    def process_result_value(self, value, dialect):
        msg = self.message_type()
        msg.ParseFromString(value)
        return msg


class AdminConfig(Base):
    __tablename__ = 'admin_configs'

    id = Column(types.Integer, primary_key=True, autoincrement=False)
    source_bans = Column(types.Boolean, nullable=False)
    is_blocked = Column(types.Boolean, nullable=False)


class PendingAction(Base):
    __tablename__ = 'pending_actions'

    id = Column(types.Integer, primary_key=True)
    timestamp = Column(types.DateTime(timezone=True), nullable=False)
    data = Column(Protobuf(proto.Action), nullable=False)


class Tag(Base):
    __tablename__ = 'tags'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    tag = Column(types.String(2000), primary_key=True)
    response = Column(types.String(2000), nullable=False)


class EscalationEntry(Base):
    __tablename__ = 'escalation_histories'

    id = Column(types.Integer, primary_key=True, autoincrement=True)
    guild_id = Column(types.BigInteger, nullable=False)
    subject_id = Column(types.BigInteger, nullable=False)
    authorizer_id = Column(types.BigInteger, nullable=False)
    authorizer_name = Column(types.String(255), nullable=False)
    display_name = Column(types.String(2000), nullable=False)
    timestamp = Column(types.DateTime(timezone=True), nullable=False)
    action = Column(Protobuf(proto.ActionSet), nullable=False)
    level_delta = Column(types.Integer, nullable=False)


class Ban(Base):
    __tablename__ = 'bans'

    guild_id = Column(types.Integer, primary_key=True)
    user_id = Column(types.BigInteger, nullable=False)
    reason = Column(types.Text)
    avatar = Column(types.Text)


class PendingDeescalation(Base):
    __tablename__ = 'pending_deescalations'

    user_id = Column(types.BigInteger, primary_key=True)
    guild_id = Column(types.BigInteger, primary_key=True)
    expiration = Column(types.DateTime(timezone=True), nullable=False)
    amount = Column(types.BigInteger, nullable=False)

    entry_id = Column(types.Integer, ForeignKey("escalation_histories.id"),
                      nullable=False)
    entry = relationship("EscalationEntry", backref="pending_deescalation")


class Member(Base):
    __tablename__ = 'members'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    user_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    role_ids = Column(postgresql.ARRAY(types.BigInteger), nullable=False)
    nickname = Column(types.String(32), nullable=True)


class Username(Base):
    __tablename__ = 'usernames'

    user_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    timestamp = Column(types.DateTime(timezone=True), primary_key=True)
    name = Column(types.String(32), nullable=False)
    discriminator = Column(types.Integer)

    def __str__(self):
        if self.discriminator is not None:
            name = f"{self.name}#{self.discriminator}"
        else:
            name = self.name
        return f"Username({self.user_id}, {self.timestamp}, {name})"

    def to_fullname(self):
        return (self.name
                if self.discriminator is None
                else f'{self.name}#{self.discriminator}')


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
    last_updated = Column(types.DateTime(timezone=True), nullable=False)
    channels = relationship("FeedChannel", back_populates="feed")

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


class FeedChannel(Base):
    __tablename__ = 'feed_channels'

    feed_id = Column('feed_id', types.BigInteger, ForeignKey('feeds.id'),
                     primary_key=True)
    channel_id = Column('channel_id', types.BigInteger, primary_key=True)
    feed = relationship("Feed", back_populates="channels")


Index("idx_username_user_id", Username.user_id)
UniqueConstraint(Username.user_id, Username.name,
                 Username.discriminator, name="idx_unique_username")


class Alias(Base):
    __tablename__ = 'aliases'

    guild_id = Column(types.BigInteger, primary_key=True, autoincrement=False)
    name = Column(types.String(2000), primary_key=True)
    content = Column(types.String(2000))
