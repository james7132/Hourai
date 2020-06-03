import abc
from aiohttp_oauth2 import oauth2_app


class OAuthBuilder(abc.ABC):

    def __init__(self, config):
        self.config = config

    @property
    @abc.abstractmethod
    def oauth_config(self):
        raise NotImplementedError()

    @property
    @abc.abstractmethod
    def authorize_url(self):
        raise NotImplementedError()

    @property
    @abc.abstractmethod
    def token_url(self):
        raise NotImplementedError()

    @property
    def scopes(self):
        return []

    def build(self):
        oauth_config = self.oauth_config()
        return oauth2_app(
            client_id=oauth_config.client_id,
            client_secret=oauth_config.client_secret,
            authorize_url=self.authorize_url,
            token_url=self.token_url,
            scopes=self.scopes
        )


class DiscordOAuthBuilder(OAuthBuilder):

    def oauth_config(self):
        return self.config.discord

    def authorize_url(self):
        return "https://discord.com/api/oauth2/authorize"

    def token_url(self):
        return "https://discord.com/api/oauth2/token"
