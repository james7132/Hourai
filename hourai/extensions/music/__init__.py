import asyncio
import ipaddress
import logging
import math
import socket
from .player import HouraiMusicPlayer
from .utils import time_format
from discord.ext import commands
from hourai.bot import GuildSpecificCog, CogLoadError
from hourai.utils import format
from urllib.parse import urlparse
import wavelink


log = logging.getLogger('hourai.music')


def clamp(val, min_val, max_val):
    return max(min(val, max_val), min_val)


class Music(GuildSpecificCog):

    def __init__(self, bot, guilds):
        super().__init__(bot, guilds=guilds)

        self.bot = bot
        self.config = None

        if not hasattr(bot, 'wavelink'):
            self.bot.wavelink = wavelink.Client(self.bot)

        self.bot.loop.create_task(self.start_nodes())

    async def start_nodes(self):
        await self.bot.wait_until_ready()

        self.config = self.bot.get_config_value('music')
        if self.config is None:
            self.bot.remove_cog(self)
            raise CogLoadError("Config option 'music' not found.")

        async def initialize_node(node_cfg):
            # Wavelink currently throws errors when provided with non IP hosts.
            # Workaround: convert host to an IP.
            args = node_cfg._asdict()
            try:
                ipaddress.ip_address(node_cfg.host)
            except ValueError:
                args['host'] = socket.gethostbyname(node_cfg.host)

            # Initiate our nodes. For now only using one
            await self.bot.wavelink.initiate_node(**args)

        nodes = self.bot.get_config_value('music.nodes', default=(),
                                          type=tuple)
        await asyncio.gather(*[initialize_node(node_cfg)
                               for node_cfg in nodes])

    def get_player(self, guild):
        return self.bot.wavelink.get_player(guild.id, cls=HouraiMusicPlayer)

    @staticmethod
    async def connect_player(ctx, player):
        channel = Music.get_voice_channel(ctx.guild)
        msg = None
        if channel is None:
            msg = 'No suitable channel for playing music found.'
        elif ctx.author not in channel.members:
            msg = f'You must be in `{channel.name}` to play music.'
        await (ctx.send(msg) if msg is not None else
               player.connect(channel.id))
        return msg is None

    @staticmethod
    def get_voice_channel(guild, member=None):
        channels = filter(lambda ch: ch.permissions_for(guild.me).connect,
                          guild.voice_channels)
        if member is not None:
            channels = filter(lambda ch: member in ch.members, channels)
        return next(channels, None)

    @commands.command()
    @commands.is_owner()
    @commands.guild_only()
    async def connect_(self, ctx):
        player = self.get_player(ctx.guild)
        channel = Music.get_voice_channel(ctx.guild, ctx.author)
        if channel is None:
            await ctx.send('No suitable channel for playing music found.')
            return False
        await player.connect(channel.id)

    @commands.command()
    @commands.is_owner()
    @commands.guild_only()
    async def disconnect_(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await player.disconnect()

    @staticmethod
    def is_empty_response(load_response):
        if load_response is None:
            return True
        if isinstance(load_response, list) and len(load_response) <= 0:
            return True
        if (isinstance(load_response, wavelink.TrackPlaylist) and
           len(load_response.tracks) <= 0):
            return True
        return False

    @commands.command()
    @commands.guild_only()
    async def play(self, ctx, *, query: str = ''):
        player = self.get_player(ctx.guild)
        if not query:
            await self._play_paused(ctx, player)
            return

        if (player.voice_channel is not None and
           ctx.author not in player.voice_channel.members):
            channel_name = player.voice_channel.name
            await ctx.send(
                content=f'You must be in `{channel_name}` to play music.')
            return

        msg = await ctx.send(r'**\*\*Loading...\*\***')
        for attempt in (query, f'ytsearch:{query}', f'scsearch:{query}'):
            result = await ctx.bot.wavelink.get_tracks(attempt)
            if not Music.is_empty_response(result):
                break
        if Music.is_empty_response(result):
            await msg.edit(content=f':bulb: No results found for `{query}`.')

        if not player.is_connected:
            if (not await Music.connect_player(ctx, player)):
                return

        if isinstance(result, list):
            assert len(result) > 0
            track = result[0]
            await player.enqueue(ctx.author, track)
            await msg.edit(content=f':notes: Added `{track.title}` '
                                   f'({time_format(track.duration)}) to the '
                                   f'queue.')
        elif isinstance(result, wavelink.TrackPlaylist):
            assert len(result.tracks) > 0
            # Add all tracks to the queue
            total_duration = 0
            for track in result.tracks:
                await player.enqueue(ctx.author, track)
                total_duration += track.duration
            count = len(result.tracks)
            total_duration = time_format(total_duration)
            await msg.edit(content=f':notes: Added **{count}** tracks'
                                   f'({total_duration}) to the queue.')
        else:
            await msg.edit(':x: Something went wrong!')

    async def _play_paused(self, ctx, player):
        if not player.is_connected or not player.is_paused:
            await ctx.send('Something needs to be specified to play.')
            return
        await player.set_pause(False)
        await ctx.send(f'Resumed {format.bold(player.current.name)}.')

    @commands.command()
    @commands.guild_only()
    async def pause(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.set_pause(True)
        await ctx.send(f'Paused {format.bold(str(player.current))}.')

    @commands.command()
    @commands.guild_only()
    async def stop(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.stop()
        await ctx.send(':notes: The player has stopped and the queue has been '
                       'cleared.')

    @commands.command()
    @commands.guild_only()
    async def remove(self, ctx, target: int):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        player.queue.remove(ctx.author.id, target)

    @commands.command()
    @commands.guild_only()
    async def removeall(self, ctx):
        player = self.get_player(ctx.guild)
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
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.create_queue_message(ctx.channel)

    @commands.command()
    @commands.guild_only()
    async def nowplaying(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.create_now_playing_message(ctx.channel)

    @commands.command()
    @commands.guild_only()
    async def skip(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        if ctx.author.id in player.skip_votes:
            await ctx.send("You've already voted to skip this song!")
            return

        assert player.current is not None

        track = player.current
        requestor = player.current_requestor
        assert requestor is not None

        channel_count = len(player.voice_channel_members)
        vote_count = len(player.skip_votes) + 1

        required_votes = math.ceil(channel_count * 0.5)
        skipped = player.vote_to_skip(ctx.author, required_votes)

        response = (f':notes: You voted to skip the song. `{vote_count} votes,'
                    f' {required_votes}/{channel_count} needed.`')
        if skipped:
            response += (f'\n:notes: Skipped **{track.title}** (requested by '
                         f'**{requestor.name}**)')
        await ctx.send(response)

    @commands.command()
    @commands.guild_only()
    async def shuffle(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        count = player.shuffle_user(ctx.author)
        if count == 0:
            msg = "You don't have any music in the queue!"
        else:
            msg = f":notes: Shuffled your {count} tracks in the queue!"
        await ctx.send(msg)

    @commands.command()
    @commands.guild_only()
    async def removeall(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        count = player.clear_user(ctx.author)
        if count == 0:
            msg = "You don't have any music in the queue!"
        else:
            msg = f"Removed your {count} tracks from the queue."
        await ctx.send(msg)

    @commands.command()
    @commands.guild_only()
    async def forceskip(self, ctx):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return

        track = player.current
        requestor = ctx.guild.get_member(player.current_requestor)
        assert requestor is not None

        player.play_next()
        await ctx.send(f':notes: Skipped **{track.title}** (requested by '
                       f'**{requestor.name}**)')

    @commands.command()
    @commands.guild_only()
    async def volume(self, ctx, volume: int = None):
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return

        old_volume = player.volume
        if volume is None:
            await ctx.send(f':sound: Current volume is `{old_volume}`')
            return
        volume = clamp(volume, 0, 150)
        await player.set_volume(volume)

        await ctx.send(f':sound: Volume changed from `{old_volume}` to '
                       f'`{volume}`')

    @commands.Cog.listener()
    async def on_voice_state_change(self, member, before, after):
        guild = member.guild
        player = self.get_player(guild)
        if not player.is_connected or member == guild.me:
            return

        # Kill the player when nobody else is in any voice channel in the guild
        def is_empty(voice_channel):
            members = set(voice_channel.members)
            members.remove(guild.me)
            return len(members) <= 0
        if all(is_empty(vc) for vc in guild.voice_channels):
            await player.stop()

        # Remove skip votes from those who leave
        channel = player.channel
        if (channel is not None and
           channel == before.channel and channel != after.channel):
            player.clear_vote(member)


def setup(bot):
    bot.add_cog(Music(bot, {163175631562080256}))
