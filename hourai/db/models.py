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


class DiscordResourceBase():
    pass


def DiscordResourceMixin(resource_cls, *args, **kwargs):

    class Mixin(DiscordResourceBase):

        _resource_class = resource_cls

        id = Column(BigInteger, *args, primary_key=True, autoincrement=False,
                    **kwargs)

        @abstractmethod
        def get_resource(self, bot):
            pass

        @classmethod
        def create_from_resource(cls, resource, *args, **kwargs):
            assert isinstance(resource, resource_cls)
            kwargs.update({'id': resource.id})
            return cls(*args, **kwargs)

        @classmethod
        def query_from_resource(cls, session, resource):
            assert isinstance(resource, resource_cls)
            return session.query(cls).get(resource.id)

        @classmethod
        def query_all_from_resource(cls, session, *resources):
            assert all(isinstance(r, resource_cls) for r in resources)
            resource_map = {r.id: r for r in resources}
            int_ids = set(resource_map.keys())
            results = session.query(cls).filter(cls.id.in_(int_ids)).all()
            results = {val.id: val for val in results}
            return {k: (resource_map[k], results.get(k)) for k in int_ids}

    Mixin.__name__ = 'GuildResource' + resource_cls.__name__ + 'Mixin'
    return Mixin


def GuildResourceMixin(resource_cls, *args, id_required=True, **kwargs):

    class Mixin(DiscordResourceMixin(resource_cls, *args, **kwargs)):

        @declared_attr
        def guild_id(cls):
            return Column(BigInteger, ForeignKey('guilds.id'),
                          nullable=not id_required)

        def get_guild(self, bot):
            return bot.get_guild(self.guild_id)

        def get_resource(self, bot):
            guild = self.get_guild(bot)
            return self._get_resource_impl(bot, guild)

        @abstractmethod
        def _get_resource_impl(self, bot, guild):
            pass

        @classmethod
        def from_resource(cls, resource, *args, **kwargs):
            kwargs['id'] = resource.id
            if hasattr('guild', resource):
                kwargs['guild_id'] = resource.guild.id
            return cls(*args, **kwargs)

    Mixin.__name__ = 'GuildResource' + resource_cls.__name__ + 'Mixin'
    return Mixin


class Guild(Base, DiscordResourceMixin(discord.Guild)):
    __tablename__ = 'guilds'

    def get_resource(self, bot):
        return bot.get_guild(self.id)


class Role(Base, GuildResourceMixin(discord.Role)):
    __tablename__ = 'roles'

    self_serve = Column(Boolean)

    def _get_resource_impl(self, bot, guild):
        return guild.get_role(self.id)


class Channel(Base, GuildResourceMixin(discord.TextChannel, id_required=False)):
    __tablename__ = 'channels'

    feeds = relationship("Feeds", secondary=feed_channels_table,
                         back_populates="channels")

    def get_resource(self, bot):
        return bot.get_channel(self.id)


class User(Base, DiscordResourceMixin(discord.User)):
    __tablename__ = 'users'


class Username(Base):
    __tablename__ = 'usernames'

    user_id = Column(BigInteger, ForeignKey('users.id'),
                     primary_key=True, autoincrement=False)
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


class CustomCommand(Base):
    __tablename__ = 'commands'

    guild_id = Column(BigInteger, primary_key=True, autoincrement=False)
    name = Column(String(2000), primary_key=True)
    content = Column(String(2000))


class Feed(Base):
    __tablename__ = 'feeds'
    __table_args__ = (UniqueConstraint('type', 'source'),)

    id = Column(BigInteger, primary_key=True)
    _type = Column('type', String(255), nullable=False)
    source = Column(String(8192), nullable=False)

    channels = relationship("Channels", secondary=feed_channels_table,
                            back_populates="channels")

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
    authorizer_id = Column(BigInteger, ForeignKey('users.id'), nullable=False)
    timestamp = Column(DateTime, nullable=False)

    authorizer = relationship("Users")

    message_id = Column(BigInteger)
    message_content = Column(String(2000))


class Action(Base):
    __tablename__ = 'actions'

    id = Column(BigInteger, primary_key=True)
    type = Column(String(255), nullable=False)
    state = Column(Enum(ActionState), nullable=False)
    guild_id = Column(BigInteger, ForeignKey('guilds.id'))
    user_id = Column(BigInteger, ForeignKey('users.id'))
    channel_id = Column(BigInteger, ForeignKey('channels.id'))
    role_id = Column(BigInteger, ForeignKey('roles.id'))
    start_timestamp = Column(DateTime)
    end_timestamp = Column(DateTime)
    action_metadata = Column(LargeBinary)

    guild = relationship('Guild')
    user = relationship('User')
    channel = relationship('Channel')
    role = relationship('Role')

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


def __create_resource_mapping():
    def get_all_subclasses(cls):
        subclasses = set(cls.__subclasses__())
        all_subclasses = set(subclasses)
        for subclass in subclasses:
            all_subclasses.update(get_all_subclasses(subclass))
        return all_subclasses
    resource_subclasses = get_all_subclasses(DiscordResourceBase)
    base_subclasses = get_all_subclasses(Base)
    concrete_subclasses = resource_subclasses & base_subclasses
    return {cls._resource_class: cls for cls in concrete_subclasses}


RESOURCE_CLASS_MAP = __create_resource_mapping()
print(RESOURCE_CLASS_MAP)
