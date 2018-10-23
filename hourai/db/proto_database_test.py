import unittest
from unittest.mock import MagicMock
from hourai.data.admin_pb2 import AdminConfig, ModeratedUser
from hourai.db.proto_database import ProtoDatabase


class FakeTransaction():
    def get(key):
        pass

    def put(key, value):
        pass

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        pass


class FakeLmdb():

    def __init__(self):
        pass

    def begin(self, db=None):
        return FakeTransaction()


class ProtoDatabaseTests(unittest.TestCase):

    def setUp(self):
        self.fake_lmdb = FakeLmdb()
        db = object() # Fake DB placeholder
        self.db = ProtoDatabase(self.fake_lmdb, db, AdminConfig)

    def test_transaction_context_manager_transparency(self):
        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.__enter__ = MagicMock()
        base_txn.__exit__ = MagicMock()

        try:
            with proto_txn:
                raise ValueError()
        except:
            pass

        base_txn.__enter__.assert_called()
        base_txn.__exit__.assert_called()

    def test_put_empty(self):
        key = 1337
        config = AdminConfig()

        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.put = MagicMock()

        with proto_txn:
            proto_txn.put(key, config)

        base_txn.put.assert_called_with(b'9\x05\x00\x00', b'')

    def test_put_nonempty(self):
        key = 1337
        config = AdminConfig()
        config.log_channel_id = key
        log_channel_id = 42

        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.put = MagicMock()

        with proto_txn:
            proto_txn.put(key, config)

        base_txn.put.assert_called_with(b'9\x05\x00\x00', b'\x18\xb9\n')

    def test_put_wrong_type(self):
        def test_func():
            key = 1337
            user = ModeratedUser()

            with self.db.begin() as txn:
                txn.put(key, user)

        self.assertRaises(TypeError, test_func)

    def test_get_empty(self):
        key = 1337
        config = AdminConfig()

        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.get = MagicMock(return_value=b'')

        with proto_txn:
            self.assertEqual(proto_txn.get(key), config)

        base_txn.get.assert_called_with(b'9\x05\x00\x00')

    def test_get_nonempty(self):
        key = 1337
        config = AdminConfig()
        config.log_channel_id = key

        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.get = MagicMock(return_value=b'\x18\xb9\n')

        with proto_txn:
            self.assertEqual(proto_txn.get(key), config)

        base_txn.get.assert_called_with(b'9\x05\x00\x00')

    def test_delete(self):
        key = 1337

        proto_txn = self.db.begin()
        base_txn = proto_txn.txn
        base_txn.delete = MagicMock()

        with proto_txn:
            proto_txn.delete(key)

        base_txn.delete.assert_called_with(b'9\x05\x00\x00')
