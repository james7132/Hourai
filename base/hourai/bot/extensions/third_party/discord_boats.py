from .base import ThirdPartyListingBase


class DiscordBoats(ThirdPartyListingBase):
    """Handles interactions with the discord.boats API"""

    def __init__(self, bot):
        super().__init__(bot)
        # Rate limit is 50 requests per minute, aim for slightly under that.
        # TODO(james7132): Set up proper rate limiting for these.
        self.delay = 45.0 / 60.0

    def get_token(self) -> str:
        return self.bot.config.third_party.discord_boats_token

    def get_api_endpoint(self, client_id) -> str:
        user_id = self.bot.user.id
        return f"https://discord.boats/api/v2/bot/{user_id}"

    def create_guild_count_payload(self) -> dict:
        # FIXME: This will not work when shards are split over multiple
        # processes/machines
        return { "server_count": len(self.bot.guilds) }
