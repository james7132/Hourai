import collections
import random
from discord.ext import commands
from hourai.bot import BaseCog

class MusicQueue():

    def __init__(self):
        self._queue_dict = collections.OrderedDict()

    def queue(self, key, item):
        """ Adds an item into the queue. """
        self._queue_dict.setdefault(key, []).append(item)

    def shuffle(self, key):
        """ Shuffles the items for a given key. """
        queue = self._queue_dict.get(key)
        if queue is None:
            return
        random.shuffle(queue)

    def remove(self, key, index):
        """ Removes one item from the queue for a key. """
        queue = self._queue_dict.get(key)
        if queue is None or index < 0 or index > len(queue):
            return
        return queue.pop(index)

    def remove_all(self, key):
        """ Removes all items from the queue for a key. """
        if  key in self._queue_dict:
            del self._queue_dict[key]

    def clear(self):
        """ Clears all elements in the queue. """
        self._queue_dict.clear()

    def __len__(self):
        return sum(len(queue) for queue in self._queue_dict.values())

class Music(BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.guild_queues = collections.defaultdict(MusicQueue)

    def get_voice_channel(self, guild):
        channels == filter(lambda ch: ch.permissions_for(guild.me).connect,
                           guild.voice_channesls)
        return next(channels, None)

    async def on_queue_update(self, guild, queue):
        is_empty = len(queue) <= 0
        client = ctx.guild.voice_client
        if is_empty and client is not None:
            await client.disconnect()
        if not is_empty and client is None:
            channel = get_voice_channel(guild)
            # TODO(james7132): handle if no channel is availalbe
            client = await channel.connect()

    @commands.command()
    async def play(self, ctx, target):
        # TODO(james7132): Fetch metadata about the music item here.
        queue_item = (target,)
        guild_queue = self.guild_queues[ctx.guild.id]
        guild_queue.queue(ctx.author.id, queue_item)
        await on_queue_update(ctx.guild, guild_queue)

    @commands.command()
    async def stop(self, ctx, target):
        # TODO(james7132): Fetch metadata about the music item here.
        guild_queue = self.guild_queues[ctx.guild.id]
        guild_queue.clear()
        await on_queue_update(ctx.guild, guild_queue)

    @commands.command()
    async def remove(self, ctx, target: int):
        # TODO(james7132): Fetch metadata about the music item here.
        guild_queue = self.guild_queues[ctx.guild.id]
        guild_queue.remove(ctx.author.id, target)
        await on_queu_update(ctx.guild, guild_queue)

    @commands.command()
    async def removeall(self, ctx):
        # TODO(james7132): Fetch metadata about the music item here.
        guild_queue = self.guild_queues[ctx.guild.id]
        guild_queue.remove_all(ctx.author.id)
        await on_queue_update(ctx.guild, guild_queue)

    @commands.command()
    async def queue(self, ctx):
        pass

def setup(bot):
    bot.add_cog(Music(bot))
