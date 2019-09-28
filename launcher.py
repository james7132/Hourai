import click
import logging
import hourai.config
from hourai import config, extensions, bot
from hourai.bot import Hourai
from hourai.db import Storage


def create_db_engine(connection_string):
    return create_engine(connection_string,
                         poolclass=pool.SingletonThreadPool,
                         connect_args={'check_same_thread': False})


@click.group()
@click.option('-c', '--config', 'config_path',
              default='config/hourai.jsonnet',
              envvar="HOURAI_CONFIG", type=click.Path())
@click.option('-e', '--env', default='dev',
              envvar="HOURAI_ENV", type=click.STRING)
@click.pass_context
def main(ctx, config_path, env):
    ctx.obj['config'] = hourai.config.load_config(config_path, env)
    logging.info(f"Loaded config from {config_path}. (Enviroment: {env}")
    import json
    print(json.dumps(ctx.obj['config']))


@main.command()
@click.pass_context
def run(ctx):
    conf = ctx.obj['config']
    bot = Hourai(config=conf, command_prefix=config.COMMAND_PREFIX)
    bot.load_all_extensions(extensions)
    # TODO(james7132): Remove this when possible
    bot.remove_command('help')
    bot.run(config.BOT_TOKEN, bot=True, reconnect=True)


@main.group()
@click.pass_context
def db(ctx):
    pass


@db.command()
@click.pass_context
def create(ctx):
    engine = create_db_engine('sqlite:///hourai.sqlite')
    models.Base.metadata.create_all(engine)


if __name__ == '__main__':
    main()
