from aiohttp import web
from . import oauth, guild_configs, bot


def create_app(config, **kwargs) -> web.Application:
    app = web.Application()
    app.update(kwargs)
    app.add_subapp('/oauth/discord/', oauth.DiscordOAuthBuilder(config).build())
    for comp in (guild_configs, bot):
        comp.add_routes(app, **kwargs)
    return app
