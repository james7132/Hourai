import os
import logging

# Logging Config

logging.basicConfig(level=logging.INFO)
for mod in ('sqlalchemy.engine', 'prawcore', 'aioredis'):
    logging.getLogger(mod).setLevel(logging.DEBUG)

BOT_TOKEN = ''

COMMAND_PREFIX = '~'
SUCCESS_RESPONSE = ':thumbsup:'

# Storage Configuration
DB_CONNECTION = 'sqlite://'
REDIS_CONNECTION = "redis://"

# Reddit Feed Configuration
REDDIT_USER_AGENT = "linux:discord.hourai.reddit:v2.0 (by /u/james7132)"
REDDIT_CLIENT_ID = ""
REDDIT_CLIENT_SECRET = ""
REDDIT_USERNAME = ""
REDDIT_PASSWORD = ""
REDDIT_BASE_URL = 'https://reddit.com'

REDDIT_FETCH_LIMIT = 20

FEED_FETCH_PARALLELISM = 10

# Import all enviroment variables in as module variables
globals().update(os.environ)

__PARSED_ATTRS = {
    int: ['REDDIT_FETCH_LIMIT', 'FEED_FETCH_PARALLELISM']
}

for parsed_type, attrs in __PARSED_ATTRS.items():
    for attr in attrs:
        vars()[attr] = parsed_type(vars()[attr])
