import discord
import praw
import threading
import logging
from prawcore import exceptions
from .types import FeedScanResult, Broadcast, Scanner
from datetime import datetime, timezone
from hourai.utils import format

log = logging.getLogger(__name__)

thread_locals = threading.local()

class RedditScanner(Scanner):

    def __init__(self, cog):
        super().__init__(cog, 'REDDIT')

    def get_reddit_client(self):
        bot = self.cog.bot
        bot.check_config_value('reddit')
        client = getattr(thread_locals, 'reddit_client', None)
        if client is None:
            log.info("Starting reddit client!")
            client = praw.Reddit(**bot.config.reddit._as_dict())
            thread_locals.reddit_client = client
        return client

    def get_result(self, feed):
        try:
            last_updated = feed.last_updated
            last_updated_unix = last_updated.replace(tzinfo=timezone.utc) \
                                            .timestamp()
            posts = []
            subreddit = self.get_reddit_client().subreddit(feed.source)
            for submission in subreddit.new():
                if submission.created_utc <= last_updated_unix:
                    break
                log.info(f'New Reddit Post in {feed.source}: {submission.title}')
                posts.append(Broadcast(
                    content=f'Post in /r/{submission.subreddit.display_name}:',
                    embed=self.submission_to_embed(submission)))
                created_utc = datetime.utcfromtimestamp(submission.created_utc)
                feed.last_updated = max(feed.last_updated, created_utc)
            return FeedScanResult.from_feed( feed, posts=posts,
                    is_updated=feed.last_updated > last_updated)
        except exceptions.NotFound:
            pass

    def submission_to_embed(self, submission):
        embed = discord.Embed(
                title=submission.title,
                url=config.REDDIT_BASE_URL + submission.permalink,
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
