import logging
import discord
from aiohttp import web


def passthrough_view(app, path, method='get', post_process_fn=None):

    async def view(request: web.Request):
        auth_header = request.headers.get("Authorization")
        if auth_header is None:
            raise web.HTTPUnauthorized('"Authorization" header must be set.')

        endpoint = f"https://discord.com/api/v6{path}"
        params = { "headers": { "Authorization": auth_header } }
        session = request.app['session']
        async with getattr(session, method)(endpoint, **params) as resp:
            if resp.status >= 400:
                return web.Response(status=resp.status, body=await resp.read())
            output = await resp.json()

        if post_process_fn is not None:
            post_process_fn(request, output)

        return web.json_response(output)

    app.add_routes([getattr(web, method)(path, view)])


def serialize_obj(obj, keys):
    return {key: getattr(obj, key) for key in keys}


def serialize_all(objs, keys):
    return [serialize_obj(obj, keys) for obj in objs]


def can_add_bot(guild):
    perms = discord.Permissions(permissions=guild.get('permissions', 0))
    return perms.manage_guild or guild.get('owner')


def add_routes(app, **kwargs):
    def guilds_post_process(request: web.Request, output):
        bot = request.app["bot"]
        copy = list(output)
        output.clear()
        for guild in copy:
            guild_id = int(guild["id"])
            bot_guild = bot.get_guild(guild_id)

            roles = []
            text_channels = []
            voice_channels = []

            if bot_guild is not None:
                props = ("id", "name")
                roles = serialize_all(bot_guild.roles, props),
                text_channels = serialize_all(bot_guild.text_channels, props)
                voice_channels = serialize_all(bot_guild.voice_channels, props)
            elif not can_add_bot(guild):
                continue

            guild.update(
                has_bot=bot_guild is not None,
                roles=roles,
                text_channels=text_channels,
                voice_channels=voice_channels)
            output.append(guild)

    passthrough_view(app, '/users/@me')
    passthrough_view(app, '/users/@me/guilds',
                     post_process_fn=guilds_post_process)
