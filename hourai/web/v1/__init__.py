from aiohttp import web
from . import oauth, guild_configs


def create_app(config) -> web.Application
    app = web.Application()
    app.add_subapp('/oauth/discord/', oauth.DiscordOAuthBuilder(config).build())
    guild_configs.add_routes(app)
    return app
