import asyncio
from discord.ext import tasks, commands
from hourai import bot, utils, config
from hourai.db import models
from sqlalchemy.orm import joinedload

from .reddit import RedditScanner

class Feeds(bot.BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.feed_dispatch = {
            models.FeedType.REDDIT: RedditScanner(self).scan,
        }
        self.scan_all_feeds.start()

    def cog_unload(self):
        self.scan_all_feeds.cancel()

    def scan_single_feed(self, feed):
        try:
            dispatch = self.feed_dispatch.get(feed.type)
            return dispatch(feed) if dispatch is not None else None
        except:
            msg = 'Failed to fetch posts from feed: "{}, {}"'.format(
                feed.type, feed.source)
            self.bot.logger.exception(msg)

    async def scan_feeds(self, feeds):
        loop = asyncio.get_event_loop()

        tasks = []
        for feed in feeds:
            if len(feed.channels) <= 0:
                self.bot.logger.info('Feed without channels: {}'.format(feed.id))
                continue
            tasks.append(loop.run_in_executor(None, self.scan_single_feed, feed))
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

                await scan_result.push(self.bot)

                if scan_result.is_updated:
                    session.add(scan_result.feed)
                    session.commit()
        except:
            self.bot.logger.exception('Error while scanning feeds.')
        finally:
            session.close()

    @scan_all_feeds.before_loop
    async def before_scan_all_feeds(self):
        await self.bot.wait_until_ready()


def setup(bot):
    bot.add_cog(Feeds(bot))
