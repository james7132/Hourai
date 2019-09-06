from sanic import Sanic
from sanic.views import HTTPMethodView
from sanic.response import json

app = Sanic()

class GuildView(HTTPMethodView):

    async def get(self, request, guild_id, alias):
        return json({})

class ModerationView(HTTPMethodView):

    async def get(self, request, guild_id):
        return json({})

class LoggingView(HTTPMethodView):

    async def get(self, request, guild_id):
        return json({})

class ValidationView(HTTPMethodView):

    async def get(self, request, guild_id):
        return json({})

class AliasView(HTTPMethodView):

    async def get(self, request, guild_id, alias):
        return json({})

def add_routes(routes):
    for cls, route in routes.items():
        cls.add_route(cls.as_view(), route)

add_routes({
    GuildView: '/guild/<guild_id>',
    ModerationView: '/guild/<guild_id>/moderation',
    LoggingView: '/guild/<guild_id>/logging',
    ValidationView: '/guild/<guild_id>/validation',
    AliasView: '/guild/<guild_id>/alias/<alias>',
})
