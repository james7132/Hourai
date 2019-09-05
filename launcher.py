import click
import os
import logging
from hourai import config, extensions
from hourai.bot import Hourai
from hourai.db import models
from sqlalchemy.pool import SingletonThreadPool
from sqlalchemy import create_engine, orm

log = logging.getLogger(__name__)

def create_db_engine(connection_string):
    return create_engine(connection_string,
                         poolclass=SingletonThreadPool,
                         connect_args={'check_same_thread': False})

@click.group()
def main():
    pass


@main.command()
def run():
    engine = create_db_engine('sqlite:///hourai.sqlite')
    session_class = orm.sessionmaker(bind=engine)
    bot = Hourai(command_prefix=config.COMMAND_PREFIX,
                 session_class=session_class,
                 max_messages=500000)
    bot.load_all_extensions(extensions)
    # TODO(james7132): Remove this when possible
    bot.remove_command('help')
    bot.run(config.BOT_TOKEN, bot=True, reconnect=True)


@main.group()
def db():
    pass


@db.command()
def create():
    engine = create_db_engine('sqlite:///hourai.sqlite')
    models.Base.metadata.create_all(engine)


@db.command()
def backup():
    pass


if __name__ == '__main__':
    main()
