import click
import logging
import hourai.config
from hourai import config, extensions, bot
from hourai.bot import Hourai
from hourai.db.storage import Storage
from hourai.db.models import Base
from sqlalchemy import create_engine, select


@click.group()
@click.option('-c', '--config', 'config_path',
              default='config/hourai.jsonnet',
              envvar="HOURAI_CONFIG", type=click.Path())
@click.option('-e', '--env', default='dev',
              envvar="HOURAI_ENV", type=click.STRING)
@click.pass_context
def main(ctx, config_path, env):
    ctx.obj = {}
    ctx.obj['config'] = hourai.config.load_config(config_path, env)
    logging.debug(str(ctx.obj['config']))
    logging.info(f"Loaded config from {config_path}. (Environment: {env})")


@main.command()
@click.pass_context
def run(ctx):
    conf = ctx.obj['config']
    hourai_bot = Hourai(config=conf)
    hourai_bot.load_all_extensions(extensions)
    hourai_bot.run(conf.bot_token, bot=True, reconnect=True)


@main.command()
@click.pass_context
def db(ctx):
    pass

# @main.command()
# @click.pass_context
# @click.option('engine')
# def create(ctx, engine):
    # storage = Storage(ctx.obj['config'])
    # storage.ensure_created()

# @main.command()
# @click.pass_context
# @click.argument('src')
# @click.argument('dst')
# def move(ctx, src, dst):
    # storage = Storage(ctx.obj['config'])
    # src_engine = storage._create_sql_engine(src)
    # dst_engine = storage._create_sql_engine(dst)

    # with src_engine.connect() as src_conn:
        # with dst_engine.connect() as dst_conn:
            # for table in Base.metadata.sorted_tables:
                # data = [dict(row) for row in src_conn.execute(select(table.c))]
                # dst_engine.execute(table.insert().values(data))


if __name__ == '__main__':
    main()
