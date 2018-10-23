import sys
import logging
import lmdb
import hourai.config as config
from hourai.db.proto_database import ProtoDatabase, short_id, guild_resource_id
from hourai.data.admin_pb2 import AdminConfig, ModeratedUser
from hourai.data.actions_pb2 import Action

log = logging.getLogger(__name__)

def init():
    log.info('Initalizing database...')
    global lmdb_env
    lmdb_env = lmdb.open(config.DB_LOCATION,
                         map_size=config.DB_MAX_SIZE,
                         max_dbs=1024)

    module = sys.modules[__name__]

    # Child databases
    databases = [
        ('admin_configs', AdminConfig, {}),
        ('moderated_users', ModeratedUser, {}),
        ('action_logs', Action, {}),
        ('scheduled_actions', Action, {}),
    ]

    for name, proto, db_opts in __DATABASES__:
        log.info(f'Creating database mapping "{name}" -> {proto}...')
        db = lmdb_env.open_db(name.encode(), **db_opts)
        setattr(module, name, ProtoDatabase(lmdb_env, db, proto))

    log.info('Database initialized.')
