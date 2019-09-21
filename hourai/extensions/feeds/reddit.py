import discord
import praw
import threading
import logging
from .types import FeedScanResult, Broadcast
from datetime import datetime
from hourai import config
from hourai.utils import format

log = logging.getLogger(__name__)

thread_locals = threading.local()

class RedditScanner():

    def __init__(self, cog):
        self.bot = cog.bot

    def get_reddit_client(self):
        client = getattr(thread_locals, 'reddit_client', None)
        if client is None:
            log.info("Starting reddit client!")
            reddit_args = {
                'client_id': config.REDDIT_CLIENT_ID,
                'client_secret': config.REDDIT_CLIENT_SECRET,
                'user_agent': config.REDDIT_USER_AGENT,
            }
            if config.REDDIT_USERNAME and config.REDDIT_PASSWORD:
                reddit_args.update({
                    'username': config.REDDIT_USERNAME,
                    'password': config.REDDIT_PASSWORD
                })
            client = praw.Reddit(**reddit_args)
            thread_locals.reddit_client = client
        return client

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

    def scan(self, feed):
        subreddit = self.get_reddit_client().subreddit(feed.source)
        latest_posts = filter(lambda s: datetime.utcfromtimestamp(s.created_utc) > feed.last_updated,
                              subreddit.new(limit=config.REDDIT_FETCH_LIMIT))
        latest_posts = sorted(latest_posts, key=lambda s: s.created_utc)

        last_updated = feed.last_updated
        posts = []
        for submission in latest_posts:
            log.info('New Reddit Post in {}: {}'.format(
                feed.source, submission.title))
            posts.append(Broadcast(
                content='Post in /r/{}:'.format(submission.subreddit.display_name),
                embed=self.submission_to_embed(submission)))
            feed.last_updated = max(feed.last_updated,
                                    datetime.utcfromtimestamp(submission.created_utc))
        return FeedScanResult(feed=feed, posts=posts,
                              is_updated=feed.last_updated > last_updated)
