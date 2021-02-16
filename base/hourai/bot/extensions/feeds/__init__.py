import asyncio
from discord.ext import tasks
from hourai.bot import cogs
from .reddit import RedditScanner


class Feeds(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.scanners = (RedditScanner(self),)
        self.scan_all_feeds.start()

    def cog_unload(self):
        self.scan_all_feeds.cancel()

    @tasks.loop(seconds=60.0)
    async def scan_all_feeds(self):
        session = self.bot.create_storage_session()
        try:
            await asyncio.gather(*[scanner.run() for scanner in self.scanners])
        except Exception:
            self.bot.logger.exception('Error while scanning feeds.')
        finally:
            session.close()

    @scan_all_feeds.before_loop
    async def before_scan_all_feeds(self):
        await self.bot.wait_until_ready()


def setup(bot):
    bot.add_cog(Feeds(bot))
