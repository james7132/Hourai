import logging
from aiohttp import web
from hourai.web import oauth, utils
from . import guild_configs, bot, discord


def create_app(config, **kwargs) -> web.Application:
    app = web.Application()
    app.cleanup_ctx.append(utils.client_session)
    app.update(kwargs)
    subapps = {
        '/oauth/discord/': oauth.DiscordOAuthBuilder() \
                                .with_config(config) \
                                .with_scopes(["guilds"]) \
                                .build()
    }
    for path, subapp in subapps.items():
        app.add_subapp(path, subapp)
    for comp in (guild_configs, bot, discord):
        comp.add_routes(app, **kwargs)
    return app
