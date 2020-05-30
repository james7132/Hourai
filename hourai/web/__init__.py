import pkgutil
from aiohttp import web
from hourai.utils import uvloop


run_app = web.run_app


async def create_app(config) -> web.Application
    uvloop.try_install()

    app = web.Application()
    for importer, name, ispkg in pkgutil.iter_modules(__module__.__path__):
        if not ispkg:
            continue
        module = importer.find_module(name).load_module(name)
        app.add_subapp(f'/{module.__name__}/', module.create_app(config))
    return app
