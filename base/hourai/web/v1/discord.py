import logging
from aiohttp import web

def passthrough_view(app, path, method='get', post_process_fn=None):

    async def view(request: web.Request):
        auth_header = request.headers.get("Authorization")
        if auth_header is None:
            raise web.HTTPUnauthorized('"Authorization" header must be set.')

        endpoint = f"https://discord.com/api/v6{path}"
        logging.debug(endpoint)
        params = { "headers": { "Authorization": auth_header } }
        session = request.app['session']
        async with getattr(session, method)(endpoint, **params) as resp:
            output = await resp.json()

        if post_process_fn is not None:
            post_process_fn(request, output)

        return web.json_response(output)

    app.add_routes([getattr(web, method)(path, view)])


def add_routes(app, **kwargs):
    def guilds_post_process(request: web.Request, output):
        bot = request.app["bot"]
        copy = list(output)
        output.clear()
        for guild in copy:
            guild_id = int(guild["id"])
            if bot.get_guild(guild_id) is not None:
                output.append(guild)

    passthrough_view(app, '/users/@me')
    passthrough_view(app, '/users/@me/guilds',
                     post_process_fn=guilds_post_process)
