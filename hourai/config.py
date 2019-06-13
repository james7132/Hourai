import os
import logging

logging.basicConfig(level=logging.INFO)

BOT_TOKEN = ''

COMMAND_PREFIX = '~'
SUCCESS_RESPONSE = ':thumbsup:'

DB_LOCATION = 'hourai.db'

# Import all enviroment variables in as module variables
globals().update(os.environ)
