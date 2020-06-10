import logging
from . import util
from aiohttp import web
from hourai.db import storage, proto
from hourai.db.storage import GuildPrefix
from hourai.web import formatters

log = logging.getLogger(__name__)

def guild_config_view(storage, field, model_type, formatter,
                      validator=None):
    cache = getattr(storage, field)

    class GuildConfigView(web.View):

        @property
        def guild_id(self):
            try:
                return int(self.request.match_info["guild_id"])
            except:
                raise web.HTTPBadRequest("Cannot parse guild id.")

        async def get(self):
            log.info(str(formatter))
            guild_id = self.guild_id
            try:
                model = await cache.get(self.guild_id)
                if model is None:
                    raise web.HTTPForbidden()
                return formatter(200).format_response(model)
            except:
                log.exception('Error:')
                raise web.HTTPInternalServerError()

        async def post(self):
            # TODO(james7132): Remove this
            return web.Response(status=403)
            guild_id = self.guild_id
            model = util.protobuf_json_request(self.request, model_type)
            try:
                # TODO(james7132): Run validation
                await cache.set(guild_id, model)
                return web.Response(status=200)
            except:
                log.exception('Error:')
                raise web.HTTPInternalServerError()

    GuildConfigView.__name__ += "_" + model_type.__name__
    return GuildConfigView


SUBFORMATTERS = {
    '': formatters.JsonProtobufFormatter,
    '.json': formatters.JsonProtobufFormatter,
    '.pb': formatters.BinaryProtobufFormatter,
    '.pbtxt': formatters.TextProtobufFormatter,
}


def __make_routes(prefix, view_constructor):
    routes = ((prefix + suffix, view_constructor(formatter))
               for suffix, formatter in SUBFORMATTERS.items())
    return [web.view(route, view) for route, view in routes]


def add_routes(app, **kwargs):
    storage = app.get('storage')
    if storage is None:
        log.warning('[Web] No storage found. Skipping guild config routes.')
        return
    route = '/guild/{guild_id:\d+}'

    routes = __make_routes(route,
            lambda formatter: guild_config_view(storage, 'guild_configs',
                proto.GuildConfig, formatter))

    for prefix in GuildPrefix:
        cache_config = prefix.value
        if cache_config.proto_type is None:
            continue
        field = prefix.name.lower() + "s"
        view_route = f'{route}/{field.replace("_configs", "")}'
        routes += __make_routes(view_route,
                lambda formatter: guild_config_view(storage, field,
                    cache_config.proto_type, formatter))
    app.add_routes(routes)
