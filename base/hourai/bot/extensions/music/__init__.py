import asyncio
import discord
import ipaddress
import logging
import math
import socket
import typing
import wavelink
from .player import HouraiMusicPlayer, Unauthorized
from .utils import time_format
from discord.ext import commands
from hourai.bot import cogs
from hourai.utils import format, is_moderator, clamp


log = logging.getLogger('hourai.music')


async def get_dj_roles(ctx):
    """Gets the corresponding DJ role for a guild. Returns none if the role is
    not found or if no role has been configured.
    """
    music_config = await ctx.guild_proxy.get_config('music')
    roles = [ctx.guild.get_role(role_id)
             for role_id in music_config.dj_role_id]
    return set(role for role in roles if role is not None)


async def is_dj(ctx, member=None):
    """ Checks if the user is a DJ or not. """
    if member is None:
        member = ctx.author
    dj_roles = await get_dj_roles(ctx)
    if len(dj_roles) <= 0:
        return is_moderator(member)
    member_roles = set(member.roles)
    return len(dj_roles.intersection(member_roles)) > 0


def get_default_channel(guild, member=None):
    channels = filter(lambda ch: ch.permissions_for(guild.me).connect,
                      guild.voice_channels)
    if member is not None:
        channels = filter(lambda ch: member in ch.members, channels)
    return next(channels, None)


async def get_voice_channel(ctx):
    music_config = await ctx.guild_proxy.get_config('music')
    channel = None
    if music_config.HasField('voice_channel_id'):
        channel = ctx.guild.get_channel(music_config.voice_channel_id)
        if isinstance(channel, discord.VoiceChannel):
            channel = None
    return channel or get_default_channel(ctx.guild, ctx.author)


class Music(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot
        self.config = None

        if not hasattr(bot, 'wavelink'):
            self.bot.wavelink = wavelink.Client(bot=self.bot)

        self.bot.loop.create_task(self.start_nodes())

    async def start_nodes(self):
        await self.bot.wait_until_ready()

        self.config = self.bot.get_config_value('music')
        if self.config is None:
            self.bot.remove_cog(self)
            raise cogs.CogLoadError("Config option 'music' not found.")

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

    async def cog_check(self, ctx):
        if ctx.guild is None:
            return False
        music_config = await ctx.guild_proxy.get_config('music')
        if music_config is None:
            return True
        # If a specific text channel is required
        if (len(music_config.text_channel_id) <= 0 or
           ctx.channel.id in music_config.text_channel_id):
            return True
        return False

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

        # Remove skip votes from those who leave after 5 minutes
        channel = player.channel
        if (channel is not None and
           channel == before.channel and channel != after.channel):
            await asyncio.sleep(300)
            if member.id not in channel.voice_states:
                player.clear_vote(member)
                await player.clear_user(member)

    @staticmethod
    async def connect_player(ctx, player):
        channel = await get_voice_channel(ctx)
        msg = None
        if channel is None:
            msg = 'No suitable channel for playing music found.'
        elif ctx.author not in channel.members:
            msg = f'You must be in `{channel.name}` to play music.'
        await (ctx.send(msg) if msg is not None else
               player.connect(channel.id))
        return msg is None

    @commands.command()
    @commands.is_owner()
    async def connect_(self, ctx):
        player = self.get_player(ctx.guild)
        channel = await get_voice_channel(ctx)
        if channel is None:
            await ctx.send('No suitable channel for playing music found.')
            return False
        await player.connect(channel.id)

    @commands.command()
    @commands.is_owner()
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

    @commands.command(name='play')
    async def play(self, ctx, *, query: str = ''):
        """Adds a song to the queue.

        Caller must be in a valid voice channel.
        If the music player is paused, calling it without arguments will
        unpause the player.

        Examples:
          ~play
          ~play despacito
          ~play https://www.youtube.com/watch?v=kJQP7kiw5Fk
          ~play https://www.youtube.com/playlist?list=PLNCRTSKrIMvuoD5D1FIR5kJ1jhwVVU5Ka
          ~play https://soundcloud.com/kungfu-cthulhu/gabenhardstyle
        """
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
            await msg.edit(content=':x: Something went wrong!')

    async def _play_paused(self, ctx, player):
        if not player.is_connected or not player.paused:
            await ctx.send('Something needs to be specified to play.')
            return
        if is_dj(ctx):
            await player.set_pause(False)
            await ctx.send(f'Resumed {format.bold(player.current.name)}.')
        else:
            await ctx.send(f'Only a DJ can resume a track.')

    @commands.command()
    @commands.check(is_dj)
    async def pause(self, ctx):
        """Pauses the current track in the music player.

        Caller must be a DJ (defaults to moderator roles), and must be in the
        same voice channel as the bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.set_pause(True)
        await ctx.send(f'Paused {format.bold(str(player.current))}.')

    @commands.command()
    @commands.check(is_dj)
    async def stop(self, ctx):
        """Clears the queue and stops the bot.

        Caller must be a DJ (defaults to moderator roles), and must be in the
        same voice channel as the bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.stop()
        await ctx.send(':notes: The player has stopped and the queue has been '
                       'cleared.')

    @commands.command()
    async def remove(self, ctx, target: int):
        """Removes a item from the queue by it's place in it.

        Caller must either be a DJ (defaults to moderator roles) or be the user
        who requested the track. Must also be in the same voice channel as the
        bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        try:
            _, track = await player.remove_entry(ctx.author, target - 1)
            await ctx.send(f"Removed **{track.title}** from the queue.")
        except Unauthorized:
            await ctx.send(f"You didn't request that track!")
        except IndexError:
            await ctx.send(f"There is no track at place: {target}")

    @commands.command()
    async def removeall(self, ctx):
        """Removes all of the caller's songs from the queue.

        Must be in the same voice channel as the bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        count = await player.clear_user(ctx.author)
        if count <= 0:
            await ctx.send("You dont' have any songs queued.")
        else:
            await ctx.send(f'Removed {count} songs from the queue.')

    @commands.command()
    async def queue(self, ctx):
        """Shows what is in the queue and who requested each track."""
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.create_queue_message(ctx.channel)

    @commands.command(aliases=['np'])
    async def nowplaying(self, ctx):
        """Shows what's currently playing in the music player."""
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return
        await player.create_now_playing_message(ctx.channel)

    @commands.command()
    async def skip(self, ctx):
        """Votes to skip the current song in the player.

        Skips the song if the votes exceed 50% of the users in voice chat.
        If the requestor skips, it automatically skips the current song.

        Must be in the same voice channel as the bot.
        """
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

        channel_count = len([m for m in player.voice_channel_members
                             if m != ctx.guild.me])
        vote_count = len(player.skip_votes) + 1

        required_votes = math.ceil(channel_count * 0.5)
        skipped = await player.vote_to_skip(ctx.author, required_votes)

        response = (f':notes: You voted to skip the song. `{vote_count} votes,'
                    f' {required_votes}/{channel_count} needed.`')
        if skipped:
            response += (f'\n:notes: Skipped **{track.title}** (requested by '
                         f'**{requestor.name}**)')
        await ctx.send(response)

    @commands.command()
    async def shuffle(self, ctx):
        """Shuffles the songs the caller has queued.

        Must be in the same voice channel as the bot.
        """
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
    @commands.check(is_dj)
    async def forceskip(self, ctx):
        """Forcibly skips the current song in the music player.

        Caller must be a DJ (defaults to moderator roles), and must be in the
        same voice channel as the bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return

        track = player.current
        requestor = player.current_requestor
        assert requestor is not None

        await player.play_next()
        await ctx.send(f':notes: Skipped **{track.title}** (requested by '
                       f'**{requestor.name}**)')

    @commands.command()
    async def volume(self, ctx, volume: typing.Optional[int] = None):
        """Checks or sets the music volume in the bot. Range: 0-150.

        Example:
          ~volume
          ~volume 40

        Caller must be a DJ (defaults to moderator roles), and must be in the
        same voice channel as the bot.
        """
        player = self.get_player(ctx.guild)
        if not player.is_connected:
            await ctx.send('There is currently no music playing.')
            return

        old_volume = player.volume
        if volume is None:
            await ctx.send(f':sound: Current volume is `{old_volume}`')
            return
        if not (await is_dj(ctx)):
            await ctx.send('Must be a DJ to change the volume.')
            return

        volume = clamp(volume, 0, 150)
        await player.set_volume(volume)

        await ctx.send(f':sound: Volume changed from `{old_volume}` to '
                       f'`{volume}`')


def setup(bot):
    bot.add_cog(Music(bot))
