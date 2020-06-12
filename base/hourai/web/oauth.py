import abc
import logging
from aiohttp import web, ClientSession, FormData


routes = web.RouteTableDef()

def redirect_uri(request):
    return str(request.url.with_path(str(request.app.router[""].url_for())))


async def client_session(app: web.Application):
    async with ClientSession() as session:
        app["session"] = session
        yield


class ViewBase(web.View):

    def get_config(self, key: str):
        return self.request.app[key]

    def handle_result(self, result):
        if 'error' in result:
            return self.handle_error(result)
        else:
            return self.handle_success(result)

    def handle_error(self, error):
        handler = self.request.app.get("ON_ERROR")
        if handler is not None:
            return handler(error, self.request)
        raise web.HTTPInternalServerError(text=f"Unhandled OAuth2 Error: {error}")

    async def handle_success(self, user_data):
        handler = self.request.app.get("ON_LOGIN")
        if handler is not None:
            return await handler(self.request, user_data)
        return web.json_response(user_data)


@routes.view("/token")
class AuthView(ViewBase):

    async def post(self) -> web.Response:
        data = await self.request.json()
        code = data.get("code")
        if code is None:
            raise web.HTTPBadRequest("Data must contain key 'code'")

        params = {
            "headers": {
                "Accept": "application/json",
                "Content-Type": "application/x-www-form-urlencoded"
            },
            "data": FormData(tuple({
                "client_id": self.get_config("CLIENT_ID"),
                "client_secret": self.get_config("CLIENT_SECRET"),
                "redirect_uri": self.get_config("REDIRECT_URI"),
                "code": code,
                "grant_type": "authorization_code",
            }.items()))
        }

        url = self.get_config("TOKEN_URL")
        async with self.get_config("session").post(url, **params) as r:
            result = await r.json()
        return await self.handle_result(result)


@routes.view("/refresh", name="refresh")
class RefreshView(ViewBase):

    async def get(self) -> web.Response:
        refresh_token = self.request.app["GET_REFRESH_TOKEN"](self.request)

        if refresh_token is None:
            raise web.HTTPUnauthorized()

        params = {
            "headers": {
                "Accept": "application/json",
                "Content-Type": "application/x-www-form-urlencoded"
            },
            "data": FormData(tuple({
                "client_id": self.request.app["CLIENT_ID"],
                "client_secret": self.request.app["CLIENT_SECRET"],
                "code": self.request.query["code"],
                "refresh_token": refresh_token,
                "redirect_uri":self.request.app["REDIRECT_URI"],
                "grant_type": "refresh_token",
            }.items()))
        }

        url = self.get_config("TOKEN_URL")
        async with self.request.app["session"].post(url, **params) as r:
            result = await r.json()
        return await self.handle_result(result)


@routes.view("/logout", name="logout")
class LogoutView(ViewBase):

    async def post(self) -> web.Response:
        refresh_token = self.request.app["GET_REFRESH_TOKEN"](self.request)
        if refresh_token is None:
            raise web.HTTPUnauthorized()
        response = web.Response(status=204)
        self.request.app["CLEAR_REFRESH_TOKEN"](response)
        return response


class OAuthBuilder(abc.ABC):

    AUTHORIZE_URL = None
    TOKEN_URL = None
    COOKIE_NAME = 'refresh_token'

    def __init__(self):
        self.config = None
        self.scopes = []

    def with_config(self, config):
        self.config = config
        return self

    def with_scopes(self, scopes):
        self.scopes = list(scopes)
        return self

    @abc.abstractmethod
    def oauth_config(self):
        raise NotImplementedError()

    async def on_login(self, request: web.Request, data):
        assert "access_token" in data
        assert "refresh_token" in data
        keys = ("access_token", "expires_in", "token_type", "scope")
        logging.debug(data)
        response = web.json_response({k:data.get(k) for k in keys})
        response.set_cookie(self.COOKIE_NAME, data['refresh_token'],
                            max_age=2592000, secure=True,
                            httponly=True, path='/api')
        return response

    async def on_error(self, error, request: web.Request):
        description = error.get("error_description", "No error description")
        error_func = {
            "invalid_request": lambda: web.HTTPBadRequest(text=description),
            "default": lambda: web.HTTPInternalServerError(
                text=f"Unhandled OAuth2 Error: {description}")
        }[error.get("error")]
        raise error_func()

    def get_refresh_token(self, request: web.Request):
        return request.cookies.get(self.COOKIE_NAME, None)

    def clear_refresh_token(self, response: web.Response):
        response.del_cookie(self.COOKIE_NAME, path='/api')

    def build(self):
        config = self.oauth_config()
        app = web.Application()
        app.update(
            CLIENT_ID=config.client_id,
            CLIENT_SECRET=config.client_secret,
            AUTHORIZE_URL=self.AUTHORIZE_URL,
            REDIRECT_URI=config.redirect_uri,
            TOKEN_URL=self.TOKEN_URL,
            SCOPES=self.scopes,
            ON_LOGIN=self.on_login,
            ON_ERROR=self.on_error,
            GET_REFRESH_TOKEN=self.get_refresh_token,
            CLEAR_REFRESH_TOKEN=self.clear_refresh_token,
        )
        app.cleanup_ctx.append(client_session)
        app.add_routes(routes)
        return app


class DiscordOAuthBuilder(OAuthBuilder):

    AUTHORIZE_URL = "https://discord.com/api/oauth2/authorize"
    TOKEN_URL = "https://discord.com/api/oauth2/token"
    COOKIE_KEY = 'discord_refresh_token'

    def oauth_config(self):
        return self.config.discord or None
