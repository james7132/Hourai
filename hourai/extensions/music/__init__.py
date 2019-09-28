import asyncio
import logging
from discord.ext import commands
from hourai import config
from hourai.utils import format
from hourai.bot import GuildSpecificCog, CogLoadError
from .player import Player
import wavelink


log = logging.getLogger(__name__)


class Music(GuildSpecificCog):

    def __init__(self, bot, guilds):
        super().__init__(bot, guild=guilds)

        self.config = config.get_config().get('music')
        if self.config is None:
            raise CogLoadError("Config option 'music' not found.")

        if not hasattr(bot, 'wavelink'):
            self.bot.wavelink = wavelink.Client(self.bot)

        self.bot.loop.create_task(self.start_nodes())

    async def start_nodes(self):
        bot.check_config_value('lavalink.nodes')

        await self.bot.wait_until_ready()

        async def iniitalize_node(node_config):
            # Initiate our nodes. For now only using one
            node = await self.bot.wavelink.initiate_node(**node_config)
            node.set_hook(self.on_wavelink_event)

        await asyncio.gather(*[initialize_node(node_cfg)
                               for node_cfg in self.config['nodes'])

    def on_lavalink_event(self, event):
        """Our event hook. Dispatched when an event occurs on our Node."""
        if isinstance(event, wavelink.TrackEnd):
            event.player.next_event.set()
        elif isinstance(event, wavelink.TrackException):
            log.error(event.error)

    @staticmethod
    def get_player(ctx):
        return ctx.bot.wavelink.get_player(ctx.guild.id, player=Player)

    @staticmethod
    async def connect_player(ctx, player):
        channel = Music.get_voice_channel(ctx.guild)
        if channel is None:
            await ctx.send('No suitable channel for playing music found.')
            return False
        if ctx.author not in channel.members:
            await ctx.send(f'You must be in `{channel.name}` to play music.')
            return False
        await player.connect(channel.id)
        return True

    @staticmethod
    def get_voice_channel(guild):
        channels = filter(lambda ch: ch.permissions_for(guild.me).connect,
                          guild.voice_channesls)
        return next(channels, None)

    @commands.command()
    @commands.guild_only()
    async def play(self, ctx, target: str = ''):
        player = Music.get_player(ctx)
        if not target:
            await self._play_paused(ctx, player)
            return

        msg = await ctx.send('**Loading...**')
        tracks = await ctx.bot.wavelink.get_tracks(f'ytsearch:{target}')
        if not tracks:
            await msg.edit(f'Could not find any songs that meet the query: **{target}**')
            return

        if not player.is_connected:
            if (not await Music.connect_player(ctx, player)):
                return

        if ctx.author not in player.channel.members:
            channel_name = player.channel.name
            await msg.edit(f'You must be in `{channel_name}` to play music.')
            return

        await asyncio.gather(
            player.play(tracks[0]),
            msg.edit(f'Added **{str(tracks[0])}** to the queue.')
        )

    async def _play_paused(self, ctx, player):
        if not player.is_connected or not player.is_paused:
            await ctx.send('Something needs to be specified to play.')
            return
        await player.set_pause(False)
        await ctx.send(f'resumed {format.bold(name)}.')

    @commands.command()
    @commands.guild_only()
    async def pause(self, ctx):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.set_pause(True)
        await ctx.send(f'Paused {format.bold(str(player.current))}.')

    @commands.command()
    @commands.guild_only()
    async def stop(self, ctx):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        player.stop()

    @commands.command()
    @commands.guild_only()
    async def remove(self, ctx, target: int):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        player.queue.remove(ctx.author.id, target)

    @commands.command()
    @commands.guild_only()
    async def removeall(self, ctx):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        count = player.clear_user(ctx.author)
        if count <= 0:
            await ctx.send("You dont' have any songs queued.")
        else:
            await ctx.send(f'Removed {count} songs from the queue.')

    @commands.command()
    @commands.guild_only()
    async def queue(self, ctx):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        raise NotImplementedError

    @commands.command()
    @commands.guild_only()
    async def skip(self, ctx):
        player = Music.get_player(ctx)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        if ctx.author.id in self.skip_votes:
            await ctx.send("You've already voted to skip this song!")
            return

        assert player.currently_playing is not None

        skipped = player.currently_playing.requestor == ctx.author

        if not skipped:
            votes = self.skip_votes[ctx.guild.id]
            votes.add(ctx.author.id)
            required_users = int(len(player.voice_client.members) * 0.5)
            skipped = len(votes) >= required_users

        if skipped:
            player.skip()
            del self.skip_votes[ctx.guild.id]

    @commands.command()
    @commands.guild_only()
    async def forceskip(self, ctx):
        player = self.guild_players.get(ctx.guild.id)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        player.skip()
        del self.skip_votes[ctx.guild.id]

    @commands.Cog.listener()
    async def on_voice_state_change(self, member, before, after):
        guild = member.guild
        player = self.guild_players.get(guild.id)
        voice_client = guild.voice_client
        if player is None or voice_client is None:
            return

        # Kill the player when nobody else is in voice
        if voice_client is not None:
            members = list(player.voice_client.members)
            if len(members) <= 0 or members == [voice_client.guild.me]:
                player.stop()

        # Remove skip votes from those who leave
        if (voice_client.channel == before.channel and
           voice_client.channel != after.channel):
            self.skip_votes.remove(member.id)


def setup(bot):
    bot.add_cog(Music(bot, {163175631562080256}))
