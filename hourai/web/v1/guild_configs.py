import logging
from . import util
from aiohttp import web
from hourai.db import storage, proto
from hourai.db.storage import GuildPrefix


log = logging.getLogger(__name__)

def guild_config_view(storage, field, model_type, validator=None):
    cache = getattr(storage, field)

    class GuildConfigView(web.View):

        @property
        def guild_id(self):
            try:
                return int(self.request.match_info["guild_id"])
            except:
                raise web.HTTPBadRequest("Cannot parse guild id.")

        async def get(self):
            guild_id = self.guild_id
            try:
                model = await cache.get(self.guild_id)
                if model is None:
                    raise web.HTTPForbidden()
                return util.protobuf_json_response(model)
            except:
                log.exception('Error:')
                raise web.HTTPInternalServerError

        async def post(self):
            guild_id = self.guild_id
            model = util.protobuf_json_request(self.request, model_type)
            try:
                # TODO(james7132): Run validation
                await cache.set(guild_id, model)
                return web.Response(status=200)
            except:
                log.exception('Error:')
                raise web.HTTPInternalServerError

    GuildConfigView.__name__ += "_" + model_type.__name__
    return GuildConfigView


def add_routes(app, storage):
    storage = app["storage"]
    route = '/guild/{guild_id:\d+}'

    view = guild_config_view(storage, 'guild_configs', proto.GuildConfig)
    routes = [web.view(route, view)]
    for prefix in GuildPrefix:
        cache_config = prefix.value
        if cache_config.proto_type is None:
            continue
        field = prefix.name.lower() + "s"
        view = guild_config_view(storage, field, cache_config.proto_type)
        view_route = f'{route}/{field.replace("_configs", "")}'
        routes.append(web.view(view_route, view))
    app.add_routes(routes)
