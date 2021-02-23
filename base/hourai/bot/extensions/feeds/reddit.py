import discord
import asyncpraw
import logging
from urllib.parse import urljoin
from asyncprawcore import exceptions
from .types import FeedScanResult, Broadcast, Scanner
from datetime import datetime, timezone
from hourai.utils import format

log = logging.getLogger('hourai.reddit')


class RedditScanner(Scanner):

    def __init__(self, cog):
        super().__init__(cog, 'REDDIT')

    @property
    def client(self):
        return self.cog.reddit_client

    def get_config_value(self, *args, **kwargs):
        return self.bot.get_config_value(*args, **kwargs)

    async def get_result(self, feed):
        try:
            last_updated = feed.last_updated
            subreddit = await self.client.subreddit(feed.source)
            posts, feed.last_updated = \
                    await self.make_posts(subreddit.new(), feed.last_updated)

            return FeedScanResult.from_feed(
                feed, posts=posts,
                is_updated=feed.last_updated > last_updated)
        except (exceptions.NotFound, exceptions.Forbidden):
            pass

    async def make_posts(self, submissions, last_updated):
        last_updated_unix = last_updated.replace(tzinfo=timezone.utc) \
                                        .timestamp()
        posts = []
        async for submission in submissions:
            if submission.created_utc <= last_updated_unix:
                break
            sub_name = submission.subreddit.display_name
            log.debug(f'New reddit post in {sub_name}: {submission.name}')
            posts.append(Broadcast(
                content=f'Post in /r/{submission.subreddit.display_name}:',
                embed=self.submission_to_embed(submission)))
            last_updated = max(last_updated,
                               datetime.utcfromtimestamp(
                                   submission.created_utc))

        # Posts are added in time descending order and are needed in ascending
        posts.reverse()

        return posts, last_updated

    def submission_to_embed(self, submission):
        base_url = self.get_config_value('reddit.base_url',
                                         default='https://reddit.com/')
        embed = discord.Embed(
            title=submission.title,
            url=urljoin(base_url, submission.permalink),
            timestamp=datetime.utcfromtimestamp(submission.created_utc),
            colour=0xFF4301)
        if submission.author is not None:
            embed.set_author(name=submission.author.name)
        self.build_embed_link(submission, embed)
        if submission.link_flair_text is not None:
            embed.set_footer(text=submission.link_flair_text)
        return embed

    def build_embed_link(self, submission, embed):
        if submission.is_self:
            embed.description = format.ellipsize(submission.selftext)
            return
        try:
            post_hint = submission.post_hint
            if post_hint == 'image':
                embed.set_image(url=submission.url)
            else:
                embed.description = submission.url
        except AttributeError:
            embed.description = submission.url
