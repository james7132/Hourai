from aiohttp import ClientSession, web

async def client_session(app: web.Application):
    async with ClientSession() as session:
        app["session"] = session
        yield
