import os
import logging
import hourai.config
from hourai.bot import Hourai

base = os.environ.get('HOURAI_CONFIG', '/etc/hourai')
env = os.environ.get('HOURAI_ENV', 'dev')
config_path = os.path.join(base, env)
conf = hourai.config.load_config(config_path)
logging.info(f"Loaded config from {config_path}. (Environment: {env})")
logging.debug(str(conf))
hourai_bot = Hourai(config=conf)
hourai_bot.load_all_extensions()
hourai_bot.run(conf.discord.bot_token, bot=True, reconnect=True)
