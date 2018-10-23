import click
import os
import logging
import hourai.data
import hourai.db as db
from hourai import config
from hourai.bot import Hourai

@click.group()
def main():
    pass

@main.command()
def run():
    bot = Hourai(command_prefix=config.COMMAND_PREFIX)

    extensions = map(lambda x: config.EXTENSION_PREFIX + x, config.EXTENSIONS)

    for extension in extensions:
        try:
            bot.load_extension(extension)
            logging.info(f'Loaded extension: {extension}')
        except:
            logging.exception(f'Failed to load extension: {extension}')

    db.init()

    bot.run(config.BOT_TOKEN,
            bot=True,
            reconnect=True)

@main.group()
def db():
    pass

@db.command()
def backup():
    pass

if __name__ == '__main__':
    main()
