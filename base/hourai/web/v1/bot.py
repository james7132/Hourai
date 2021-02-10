import logging
import collections
from aiohttp import web
from hourai.bot import CounterKeys


log = logging.getLogger(__name__)


def add_routes(app, **kwargs):
    bot = app.get("bot")
    if bot is None:
        log.warning('[Web] No bot provided, bot status endpoints not '
                    'included.')
        return

    class BotStatus(web.View):

        async def get(self):
            latencies = dict(bot.latencies)
            stats = dict()
            for shard_id in latencies:
                shard_status = dict(self.get_shard_stats(shard_id))
                shard_status['latency'] = latencies[shard_id]
                stats[shard_id] = shard_status
            return web.json_response({"shards": stats})

        def get_shard_stats(self, shard_id):
            counters = collections.Counter()
            counters['Shard'] = shard_id
            for guild in bot.guilds:
                if guild.shard_id != shard_id:
                    continue
                guild_counts = bot.guild_counters[guild.id]
                counters['guilds'] += 1
                counters['members'] += guild.member_count
                counters['messages'] += guild_counts[
                        CounterKeys.MESSAGES_RECIEVED]
                if any(guild.me in vc.members for vc in guild.voice_channels):
                    counters['music'] += 1
            return counters

    app.add_routes([web.view('/bot/status', BotStatus)])
