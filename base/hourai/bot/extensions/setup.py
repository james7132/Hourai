import asyncio
import discord
import logging
from discord.ext import commands
from hourai.bot import cogs
from hourai.db import proxies


log = logging.getLogger(__name__)


def create_text_channel_search(name):
    def search(ch, me):
        perms = ch.permissions_for(me)
        return name in ch.name.lower() and \
            perms.read_messages and \
            perms.send_messages
    return search


class Setup(cogs.BaseCog):
    """ Cog for automated guild configuration based on defaults. """

    def __init__(self, bot):
        self.bot = bot

    @commands.Cog.listener()
    async def on_guild_join(self, guild):
        with self.bot.create_storage_session() as session:
            config = session.query(models.AdminConfig).get(guild.id)
            if config is not None and config.is_blocked:
                await guild.leave()
                return

            tasks = [self.__setup_modlog(session, guild)]
            await asyncio.gather(*tasks)

    async def __setup_modlog(self, session, guild):
        proxy = self.bot.create_guild_proxy(guild)
        config = await proxy.get_config('logging')

        # Only configure this if it isn't set
        if config is not None and config.HasField('modlog_channel_id'):
            return

        modlog_channel = discord.utils.find(
            lambda ch: create_text_channel_search('modlog')(ch, guild.me),
            guild.text_channels)

        if modlog_channel is None:
            log.info(
                f'Skipping automatic modlog setup: Could not find a modlog '
                f'channel for guild: {guild.id}')
            return

        await proxy.set_modlog_channel(modlog_channel)

        log.info(
            f'Automatic Setup: Set modlog for guild {guild.id} to channel '
            f'{modlog_channel.id}')

        await modlog_channel.send(
            "**[Automatic Setup]** This channel has been automatically "
            "configured as the server as a modlog channel. If this is "
            "undesirable, please use `~setmodlog <channel>` to change it.")


class Teardown(cogs.BaseCog):
    """ Cog for automated teardown of guild related data. """

    def __init__(self, bot):
        self.bot = bot

    @commands.Cog.listener()
    async def on_guild_remove(self, guild):
        with self.bot.create_storage_session() as session:
            tasks = [
                # Clear guild configs since they avtively use RAM in Redis.
                session.guild_configs.clear(guild.id),
            ]
            await asyncio.gather(*tasks)

def setup(bot):
    bot.add_cog(Setup(bot))
    bot.add_cog(Teardown(bot))
