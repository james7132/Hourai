import logging
from collections import namedtuple
from hourai import utils

log = logging.getLogger(__name__)


class FeedScanResult(namedtuple('ResultBase', ['feed', 'posts', 'is_updated'])):

    async def push(self, bot):
        if self.feed is None:
            return

        for post in self.posts:
            await post.push(self.feed.get_channels(bot))


class Broadcast(namedtuple('BroadcastBase', ['content', 'embed'])):

    async def push(self, channels):
        try:
            await utils.broadcast(channels, content=self.content,
                                  embed=self.embed)
        except:
            log.exception('Error while broadcasting post.')
