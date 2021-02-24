import asyncio
import asyncpraw
import asyncprawcore
from datetime import datetime
from discord.ext import tasks, commands
from hourai.bot import cogs
from hourai.db import models
from .reddit import RedditScanner


class Feeds(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot

        self.bot.logger.debug("Starting reddit client.")
        conf = bot.get_config_value('reddit')
        self.reddit_client = asyncpraw.Reddit(**conf._asdict())

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

    @commands.group(name="reddit")
    async def reddit(self, ctx):
        """A group of commands for setting up reddit feeds."""
        pass

    @reddit.command(name="add")
    @commands.has_permissions(manage_guild=True)
    async def reddit_add(self, ctx, *, subreddit: str):
        """Adds a subreddit feed from the current channel.

        Supports a multireddit feed: (i.e. "funny+aww" for both /r/funny and
        /r/aww).

        Requires: Manage Server permissions.
        """
        subreddits = subreddit.split("+")
        try:
            sub = await self.reddit_client.subreddit(subreddit)
        except asyncprawcore.exceptions.NotFound:
            await ctx.send(f"No such subreddit found: {subreddit}")
            return

        channel = ctx.session.query(models.Channel).get(ctx.channel.id)
        if channel is None:
            channel = models.Channel(id=ctx.channel.id)
            ctx.session.add(channel)

        feeds = ctx.session.query(models.Feed) \
                   .filter_by(_type="REDDIT") \
                   .filter(models.Feed.source.in_(subreddits)) \
                   .all()
        seen_subs = {f.source: f for f in feeds}
        now = datetime.utcnow()
        for sub in subreddits:
            feed = seen_subs.get(sub, models.Feed(_type="REDDIT",
                                                   source=sub,
                                                   last_updated=now))
            if feed not in channel.feeds:
                channel.feeds.append(feed)

        ctx.session.commit()
        await ctx.send(f"Set up feed for `/r/{subreddit}` in this channel")

    @reddit.command(name="remove")
    @commands.has_permissions(manage_guild=True)
    async def reddit_remove(self, ctx, *, subreddit: str):
        """Removes a subreddit feed from the current channel.

        Supports multireddits (i.e. "funny+aww" for both /r/funny and
        /r/aww).

        Requires: Manage Server permissions.
        """
        subreddits = set(subreddit.split("+"))

        channel = ctx.session.query(models.Channel).get(ctx.channel.id)
        if channel is None:
            await ctx.send("No reddit feeds are configured for this channel.")
            return

        feeds = ctx.session.query(models.Feed) \
                   .filter_by(_type="REDDIT") \
                   .filter(models.Feed.source.in_(subreddits)) \
                   .all()

        for feed in feeds:
            channel.feeds.remove(feed)
            if len(feed.channels) <= 0:
                ctx.session.delete(feed)

        ctx.session.commit()
        await ctx.send(f"Removed feed for `/r/{subreddit}` from this channel")

    @reddit.command(name="list")
    async def reddit_list(self, ctx):
        """Lists all of the subreddits that feed into this channel"""
        channel = ctx.session.query(models.Channel).get(ctx.channel.id)
        if channel is None:
            await ctx.send("No reddit feeds are configured for this channel.")
            return

        subreddits = [f"`/r/{feed.source}`" for feed in channel.feeds
                      if feed._type == "REDDIT"]
        if len(subreddits) <= 0:
            await ctx.send("No reddit feeds are configured for this channel.")
            return
        await ctx.send(", ".join(subreddits))


def setup(bot):
    bot.add_cog(Feeds(bot))
