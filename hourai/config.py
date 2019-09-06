import os
import logging

# Use uvloop where possible
try:
    import uvloop
    uvloop.install()
except ImportError:
    pass

# Logging Config

logging.basicConfig(level=logging.INFO)
logging.getLogger('sqlalchemy.engine').setLevel(logging.DEBUG)
logging.getLogger('prawcore').setLevel(logging.DEBUG)

BOT_TOKEN = ''

COMMAND_PREFIX = '~'
SUCCESS_RESPONSE = ':thumbsup:'

DB_LOCATION = 'hourai.sqlite'

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
