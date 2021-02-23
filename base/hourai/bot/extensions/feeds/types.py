import logging
import asyncio
from abc import abstractmethod, ABC
from collections import namedtuple
from hourai import utils
from hourai.db import models
from sqlalchemy.orm import joinedload

log = logging.getLogger(__name__)


class FeedScanResult(namedtuple('ResultBase', ['channel_ids', 'posts',
                                               'is_updated'])):

    @staticmethod
    def from_feed(feed, **kwargs):
        kwargs['channel_ids'] = [ch.id for ch in feed.channels]
        return FeedScanResult(**kwargs)

    async def push(self, bot):
        channels = [bot.get_channel(ch_id) for ch_id in self.channel_ids]
        for post in self.posts:
            await post.push(channels)


class Broadcast(namedtuple('BroadcastBase', ['content', 'embed'])):

    async def push(self, channels):
        try:
            await utils.broadcast(channels, content=self.content,
                                  embed=self.embed)
        except Exception:
            log.exception('Error while broadcasting post.')


class Scanner(ABC):

    def __init__(self, cog, feed_type):
        self.cog = cog
        self.bot = cog.bot
        self.feed_type = feed_type

    async def run(self):
        type_name = type(self).__name__
        await self.bot.wait_until_ready()
        log.info(f'Scanning {type_name}...')

        async def scan_feed(feed, session):
            channels = list(feed.get_channels(self.bot))
            if len(channels) <= 0:
                return

            try:
                result = await self.get_result(feed)
                await self._publish(session, result, feed)
            except Exception:
                log.exception('Failure while fetching feeds:')

        with self.bot.create_storage_session() as session:
            feeds = self.get_feeds(session)
            await asyncio.gather(*[scan_feed(feed, session) for feed in feeds])

    @abstractmethod
    async def get_result(self, feed):
        raise NotImplementedError

    def get_feeds(self, session):
        query = session.query(models.Feed) \
                       .filter(models.Feed._type == self.feed_type) \
                       .options(joinedload(models.Feed.channels))
        return list(query)

    async def _publish(self, session, result, feed):
        if result is None or len(result.posts) <= 0:
            return
        await result.push(self.bot)
        if result.is_updated:
            session.add(feed)
            session.commit()
