import logging
from aiohttp import web
from hourai.web import oauth
from . import oauth, guild_configs, bot


def create_app(config, **kwargs) -> web.Application:
    app = web.Application()
    app.update(kwargs)
    subapps = {
        '/oauth/discord/': oauth.DiscordOAuthBuilder() \
                                .with_config(config) \
                                .with_scopes(["guilds"]) \
                                .build()
    }
    for path, subapp in subapps.items():
        app.add_subapp(path, subapp)
    for comp in (guild_configs, bot):
        comp.add_routes(app, **kwargs)
    return app
