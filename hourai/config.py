import os
import logging

logging.basicConfig(level=logging.INFO)

COMMAND_PREFIX = '$'
SUCCESS_RESPONSE = ':thumbsup:'

BOT_TOKEN = ''

DB_LOCATION = 'hourai.db'
DB_MAX_SIZE = 1024 ** 4 # 1TB

EXTENSION_PREFIX = 'hourai.addons.'
EXTENSIONS = [
    'admin',
    'owner',
    'servers.touhou_project'
]

# Import all enviroment variables in as module variables
globals().update(os.environ)
