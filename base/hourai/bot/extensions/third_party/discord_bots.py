from .base import ThirdPartyListingBase


class DiscordBots(ThirdPartyListingBase):
    """Handles interactions with the discord.bots.gg API"""

    def get_token(self) -> str:
        return self.bot.config.third_party.discord_bots_token

    def get_api_endpoint(self, client_id) -> str:
        user_id = self.bot.user.id
        return f"https://discord.bots.gg/api/v1/bots/{user_id}/stats"

    def create_guild_count_payload(self) -> dict:
        # FIXME: This will not work when shards are split over multiple
        # processes/machines
        return {"guildCount": len(self.bot.guilds)}
