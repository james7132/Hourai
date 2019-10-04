import click
import logging
import hourai.config
from hourai import config, extensions, bot
from hourai.bot import Hourai


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


if __name__ == '__main__':
    main()
