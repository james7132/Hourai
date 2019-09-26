import logging
import threading
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
        except:
            log.exception('Error while broadcasting post.')


class Scanner:

    def __init__(self, cog, feed_type):
        self.bot = cog.bot
        self.feed_type = feed_type
        self._stop_event = threading.Event()
        self.fetcher_thread = threading.Thread(target=self.run, args=())
        self.fetcher_thread.start()

    def run(self):
        log.info(f'{type(self).__name__} initialized. Waiting for bot to be ready.')
        self.bot.spin_wait_until_ready()
        log.info(f'{type(self).__name__} started.')
        while not self.stopped():
            with self.bot.create_storage_session() as session:
                for feed in self.get_feeds(session):
                    # log.info(f'Scanning: {feed.type.name}, {feed.source}..')
                    try:
                        result = self.get_result(feed)
                        self._publish(session, result, feed)
                    except Exception:
                        log.exception('Failure while fetching feeds:')
                    if self.stopped():
                        break
        log.info(f'{type(self).__name__}: Stopping.')
    @abstractmethod
    def get_result(self, feed):
        raise NotImplementedError

    def get_feeds(self, session):
        query = session.query(models.Feed) \
                       .filter(models.Feed._type==self.feed_type) \
                       .options(joinedload(models.Feed.channels))
        return list(query)

    def _publish(self, session, result, feed):
        if result is None or len(result.posts) <= 0:
            return
        async def callback():
            await result.push(self.bot)
            if result.is_updated:
                session.add(feed)
                session.commit()
        future = asyncio.run_coroutine_threadsafe(callback(), self.bot.loop)
        # Wait for it to publish
        future.result()

    def close(self):
        self._stop_event.set()
        self.fetcher_thread.join()

    def stopped(self):
        return self._stop_event.is_set()
