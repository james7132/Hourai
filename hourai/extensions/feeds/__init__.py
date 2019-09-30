import asyncio
from discord.ext import tasks
from hourai.cogs import BaseCog
from hourai.db import models
from sqlalchemy.orm import joinedload
from .reddit import RedditScanner


class Feeds(BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.scanners = (RedditScanner(self),)
        self.feed_dispatch = {}
        if len(self.feed_dispatch) > 0:
            self.scan_all_feeds.start()

    def cog_unload(self):
        self.scan_all_feeds.cancel()
        for scanner in self.scanners:
            scanner.close()

    def scan_single_feed(self, feed):
        try:
            dispatch = self.feed_dispatch.get(feed.type)
            return dispatch(feed) if dispatch is not None else None
        except Exception:
            msg = 'Failed to fetch posts from feed: "{}, {}"'.format(
                feed.type, feed.source)
            self.bot.logger.exception(msg)

    def update_feed(self, scan_result):
        if not scan_result.is_updated:
            return
        with self.bot.create_storage_session() as session:
            session.add(scan_result.feed)
            session.commit()

    async def scan_feeds(self, feeds):
        loop = asyncio.get_event_loop()

        tasks = []
        for feed in feeds:
            channels = list(feed.get_channels(self.bot))
            if len(channels) <= 0:
                self.bot.logger.info(f'Feed without channels: {feed.id}')
                continue
            tasks.append(
                    loop.run_in_executor(None, self.scan_single_feed, feed))
        return await asyncio.gather(*tasks)

    @tasks.loop(seconds=60.0)
    async def scan_all_feeds(self):
        self.bot.logger.info('Scanning feeds...')

        session = self.bot.create_storage_session()
        try:
            feeds = session.query(models.Feed) \
                    .options(joinedload(models.Feed.channels))

            results = await self.scan_feeds(feeds)

            # Broadcast Results
            for scan_result in results:
                if scan_result is None:
                    continue

        except Exception:
            self.bot.logger.exception('Error while scanning feeds.')
        finally:
            session.close()

    @scan_all_feeds.before_loop
    async def before_scan_all_feeds(self):
        await self.bot.wait_until_ready()


def setup(bot):
    bot.add_cog(Feeds(bot))
