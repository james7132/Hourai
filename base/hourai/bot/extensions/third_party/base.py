from abc import abstractmethod
from discord.ext import commands
from hourai.bot import cogs


class ThirdPartyListingBase(cogs.BaseCog):
    """Handles interactions with the discord.bots.gg API"""

    def __init__(self, bot):
        self.bot = bot
        self.client_id = None
        self.delay = 1.0
        self.bot.loop.create_task(self._auto_post())

    async def _auto_post(self):
        if self.get_token() is None:
            self.bot.logger.warning(
                f'No token specified for {self.qualified_name}. Disabling.')
            return

        await self.bot.wait_until_ready()
        while not self.bot.is_closed():
            try:
                await self.send_server_count()
            except Exception:
                name = self.qualified_name
                self.bot.logger.exception(
                    f'Error while sending server counts to {name}:')
            await asyncio.sleep(self.delay)

    @abstractmethod
    def get_token(self):
        raise NotImplementedError()

    @abstractmethod
    def get_api_endpoint(self, client_id):
        raise NotImplementedError()

    @abstractmethod
    def create_guild_count_payload(self):
        raise NotImplementedError()

    async def get_client_id(self):
        if self.client_id is None:
            app_info = await self.bot.application_info()
            self.client_id = app_info.id
        return self.client_id

    async def send_server_count(self):
        client_id = await self.get_client_id()
        endpoint = self.get_api_endpoint(client_id)
        params = {
            "headers": {
                "Authorization": self.get_token()
            },
            "json": self.create_guild_count_payload()
        }
        async with self.bot.http_session.post(endpoint, **params) as resp:
            response = await resp.body()
            self.bot.logger.debug(
                    f"Guild Count Posted to {endpoint} Response: {response}")
            await resp.raise_for_status()
