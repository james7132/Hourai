import enum
from hourai import config
from hourai.data import admin_pb2, models_pb2
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy import types, create_engine
from sqlalchemy import Column, Integer, LargeBinary, Enum, String, DateTime

Base = declarative_base()
engine = create_engine(config.DB_CONNECTION_STRING)


class EntityBase(Base):
    __tablename__ = 'entity'

    type = Column(String(255), primary_key=True)
    id = Column(Integer, primary_key=True, autoincrement=False)
    guild_id = Column(Integer)
    _proto = Column('data', LargeBinary, nullable=False)

    __mapper_args__ = {
        'polymorphic_on': 'type',
        'polymorphic_identity': ''
    }


def _create_proto_wrapper(proto_type):
    class ProtoWrapper():

        def __init__(self):
            self._proto_obj = None

        def _deserialize_or_create_proto(self):
            proto = proto_type()
            if self._proto is not None:
                proto.ParseFromString(self._proto)
            return proto

        @property
        def proto(self):
            if self._proto_obj is None:
                self._proto_obj = self._deserialize_or_create_proto()
            return self._proto_obj

        @proto.setter
        def proto(self, proto_obj):
            if not isinstance(proto_obj, proto_type):
                raise TypeError(
                    f'Object of type "{type(proto_obj)}" not instance of {proto_type}')
            self._proto_obj = proto_obj
            if self.proto_obj is not None:
                self._proto = self._proto_obj.SerializeToString()
    ProtoWrapper.__name__ += '_' + proto_type.__name__
    return ProtoWrapper


def entity_class(proto_type, base_class=EntityBase):
    proto_wrapper = _create_proto_wrapper(proto_type)

    def __entity(cls):
        mapper_args = {
            'polymorphic_identity': proto_type.__name__
        }
        if hasattr(cls, '__mapper_args__'):
            mapper_args.update(cls.__mapper_args__)
        attrs = {'__mapper_args__': mapper_args}
        return type(cls.__name__, (EntityBase, cls, proto_wrapper), attrs)

    return __entity


@entity_class(admin_pb2.AdminConfig)
class AdminConfig():
    pass


@entity_class(admin_pb2.ModeratedUser)
class ModeratedUser():
    pass


@entity_class(models_pb2.User)
class User():
    pass


class ModerationLog(Base):
    __tablename__ = 'ModerationLog'
    id = Column(Integer, primary_key=True, autoincrement=True)
    user_id = Column(Integer)
    guild_id = Column(Integer)
    timestamp = Column(DateTime)
    data_proto = Column(LargeBinary)
