from . import consts


async def post(session, content: str, base_url='https://hastebin.com'):
    endpoint = '{}/documents'.format(base_url)
    async with session.post(endpoint, data=content.encode('utf-8')) as request:
        return '{}/{}'.format(base_url, (await request.json())['key'])


async def str_or_hastebin_link(bot, content: str, *args, **kwargs):
    if len(content) > consts.DISCORD_MAX_MESSAGE_SIZE:
        return await post(bot.http_session, content, *args, **kwargs)
    return content
