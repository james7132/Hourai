import logging
import discord
import asyncio
import time
from aiohttp import web
from google.protobuf import json_format


log = logging.getLogger('hourai.web.discord')


def passthrough_view(app, path, method='get', post_process_fn=None):

    async def view(request: web.Request):
        auth_header = request.headers.get("Authorization")
        if auth_header is None:
            raise web.HTTPUnauthorized('"Authorization" header must be set.')

        endpoint = f"https://discord.com/api/v6{path}"
        params = {"headers": {"Authorization": auth_header}}
        session = request.app['session']

        start_time = time.time()
        async with getattr(session, method)(endpoint, **params) as resp:
            if resp.status >= 400:
                return web.Response(status=resp.status, body=await resp.read())
            output = await resp.json()
        latency = time.time() - start_time
        log.info(f'Upstream request latency for "{path}": {latency}')

        if post_process_fn is not None:
            await post_process_fn(request, output)

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
    async def guilds_post_process(request: web.Request, output):
        bot = request.app["bot"]
        copy = list(output)
        ids = [int(g["id"]) for g in output]
        output.clear()

        configs = await asyncio.gather(*[
            bot.storage.guild_configs.get(guild_id) for guild_id in ids])

        for guild_id, guild, config in zip(ids, copy, configs):
            if not can_add_bot(guild):
                continue
            bot_guild = bot.get_guild(guild_id)

            if bot_guild is None:
                guild.update(
                    has_bot=False,
                    member_count=None,
                    config=None,
                    roles=[],
                    text_channels=[],
                    voice_channels=[])
            else:
                props = ("id", "name")
                guild.update(
                    has_bot=True,
                    member_count=bot_guild.member_count,
                    roles=serialize_all(bot_guild.roles, props),
                    text_channels=serialize_all(bot_guild.text_channels,
                                                props),
                    voice_channels=serialize_all(
                        bot_guild.voice_channels, props),
                    config=json_format.MessageToDict(config))

            output.append(guild)

    passthrough_view(app, '/users/@me')
    passthrough_view(app, '/users/@me/guilds',
                     post_process_fn=guilds_post_process)
