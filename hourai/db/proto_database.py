import logging
import base64
import struct
import collections
import discord
from types import MethodType

log = logging.getLogger(__name__)


def short_id(key):
    """
    Key function for objects that are either an integer ID
    or an object with a defined ID.

    Returns a little-endian encoded joined key.
    """
    handler = __ID_TYPE_HANDLERS.get(type(key))
    if handler is not None:
        return handler(key)
    for key_type, handler in _ID_ABC_HANDLERS.items():
        if isinstance(key, key_type):
            return handler(key)
    raise TypeError()


def identity(key):
    return key


def snowflake_id(key):
    return short_id(key.id)


def guild_resource_id(key):
    return short_id((key.guild.id, key.id))


__ID_TYPE_HANDLERS = {
    int: lambda key: struct.pack('<L', key),
    str: lambda key: key.encode(),
    bytes: identity,
    memoryview: identity,
    discord.User: snowflake_id,
    discord.Guild: snowflake_id,
    discord.Member: guild_resource_id,
    discord.Role: guild_resource_id,
}

__ID_ABC_HANDLERS = [
    (collections.Iterable, lambda key: b"".join(short_id(sub) for sub in key)),
    (discord.abc.GuildChannel, guild_resource_id),
    (discord.abc.Snowflake, snowflake_id),
]


class ProtoDatabase():
    """
    A wrapper around a named lmdb database that encodes and decodes keys and 
    protobuffers
    """

    def __init__(self, env, db, proto_type, key_fn=short_id):
        self.env = env
        self.db_handle = db
        self.proto_type = proto_type
        self.key_fn = key_fn or (lambda x: str(x))

    def begin(self, *args, **kwargs):
        txn = self.env.begin(db=self.db_handle, *args, **kwargs)
        return self.transaction(txn)

    def transaction(self, txn):
        """
        Creates a ProtoDatabaseTransaction wrapper without starting a new
        transaction.

        This wrapper should not outlive the base transaction, and does not need
        to be wrapped in a with statement to ensure transaction safety.
        """
        return ProtoDatabaseTransaction(self, txn)

    def get(self, key, *args, **kwargs):
        with self.begin() as txn:
            return txn.get(key, *args, **kwargs)

    def __getattr__(self, attr):
        return getattr(self.env, attr)


class ProtoDatabaseTransaction():

    def __init__(self, db, txn):
        self.db = db
        self.txn = txn

    def _transform_key(self, key):
        db_key = self.db.key_fn(key)
        log.debug(
            f'[{self.db.proto_type}] Key transformation: "{key}" -> "{db_key}"')
        return db_key

    def get(self, key, *args, **kwargs):
        key = self._transform_key(key)
        buf = self.txn.get(key, db=self.db.db_handle, *args, **kwargs)
        if buf is None:
            log.debug(f'[{self.db.proto_type}] Key not found: "{key}"')
            return None
        proto = self.db.proto_type.FromString(buf)
        log.debug(f'[{self.db.proto_type}] Read key: "{key}" -> {proto}')
        return proto

    def put(self, key, value, *args, **kwargs):
        if not isinstance(value, self.db.proto_type):
            raise TypeError(f'Value must be of type {self.db.proto_type}')
        key = self._transform_key(key)
        proto_str = value.SerializeToString()
        self.txn.put(key, proto_str, db=self.db.db_handle, *args, **kwargs)
        log.debug(f'[{self.db.proto_type}] Put key: "{key}"')

    def delete(self, key):
        key = self._transform_key(key)
        self.txn.delete(key, db=self.db.db_handle)
        log.debug(f'[{self.db.proto_type}] Delete key: "{key}"')

    def cursor(self):
        csr = self.txn.cursor(db=self.db.db_handle)

        proto_type = self.db.proto_type

        def deserialize(key, buf):
            proto = proto_type.FromString(buf)
            log.debug(f'[{self.db.proto_type}] Read key: "{key}" -> {proto}')
            return proto

        csr_value = csr.value
        csr_item = csr.item

        def new_value(c):
            return deserialize(key(), csr_value())

        def new_item(c):
            key, value = csr_item()
            return (key, deserialize(key, value))
        csr.value = MethodType(new_value, csr)
        csr.item = MethodType(new_item, csr)

        return csr

    def __enter__(self):
        self.txn.__enter__()
        return self

    def __exit__(self, type, value, traceback):
        self.txn.__exit__(type, value, traceback)

    def __getattr__(self, attr):
        return getattr(self.txn, attr)
