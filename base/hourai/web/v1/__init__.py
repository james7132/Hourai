from aiohttp import web
from . import oauth, guild_configs


def create_app(config, storage) -> web.Application:
    app = web.Application()
    app["storage"] = storage
    app.add_subapp('/oauth/discord/', oauth.DiscordOAuthBuilder(config).build())
    guild_configs.add_routes(app, storage)
    return app
