import discord
import asyncio
import logging
import wavelink
import math
from .queue import MusicQueue
from . import utils
from hourai import utils as hourai_utils
from hourai.utils import embed
from hourai.db import proto
from wavelink.eqs import Equalizer

log = logging.getLogger('hourai.music.player')


PROGRESS_BAR_WIDTH = 12
TRACKS_PER_PAGE = 10
POST_TRACK_WAIT = 1.5


class Unauthorized(Exception):
    pass


class HouraiMusicPlayer(wavelink.Player):

    def __init__(self, bot, guild_id: int, node: wavelink.Node):
        super().__init__(bot, guild_id, node)

        self.queue = MusicQueue()
        self.guild_proxy = self.bot.get_guild_proxy(self.guild)

        self.current = None
        self._requestor_id = None

        self.skip_votes = set()
        self.ui_msgs = list()

        bot.loop.create_task(self.__init_player())

    def get_default_channel(self):
        return discord.utils.find(
                lambda ch: ch.permissions_for(self.guild.me).connect,
                self.guild.voice_channels)

    async def get_voice_channel(self):
        music_config = await self.guild_proxy.config.get('music')
        channel = None
        if music_config.HasField('voice_channel_id'):
            channel = self.guild.get_channel(music_config.voice_channel_id)
            if isinstance(channel, discord.VoiceChannel):
                channel = None
        return channel or self.get_default_channel()

    async def __init_player(self):
        await self.bot.wait_until_ready()

        await self.set_eq(Equalizer.flat())
        config = await self.guild_proxy.config.get('music')
        await self.set_volume(config.volume)

    async def disconnect(self):
        await super().disconnect()
        self.current = None
        self._requestor_id = None
        self.skip_votes.clear()

    async def hook(self, event):
        if isinstance(event, wavelink.events.TrackEnd):
            if event.reason in ('FINISHED', 'LOAD_FAILED'):
                await asyncio.sleep(POST_TRACK_WAIT)
                await self.play_next()
        elif isinstance(event, wavelink.events.TrackException):
            log.error(f"Error while playing Track {event.track} in guild "
                      f"{self.guild_id}: {event.error}")

    @property
    def guild(self):
        return self.bot.get_guild(self.guild_id)

    @property
    def voice_channel(self):
        guild = self.guild
        if guild is None:
            return None
        # Get the first voice channel where the bot user is in, default to None
        return next((vc for vc in guild.voice_channels
                     if guild.me in vc.members), None)

    @property
    def voice_channel_members(self):
        channel = self.voice_channel
        return () if channel is None else channel.members

    @property
    def entries(self):
        return list(self.queue)

    async def get_current_requestor(self):
        if self._requestor_id is None or self.guild is None:
            return None
        return await hourai_utils.get_member_async(self.guild,
                                                   self._requestor_id)

    @property
    def is_playing(self):
        return self.is_connected and self.current is not None

    async def play_next(self):
        """Plays the next song. If a song is currently playing, it will be
        skipped.
        """
        # Clear votes...
        self.skip_votes.clear()
        self.current = None
        self._requestor_id = None

        if self.is_connected and len(self.queue) <= 0:
            # Stop playing the song and disconnect
            await self.disconnect()
            await super().stop()
            return

        requestor_id, song = await self.queue.get()
        if not song:
            #TODO(james7132): Log an error here.
            return

        if not self.is_connected:
            channel = await self.get_voice_channel()
            if channel is None:
                return
            await self.connect(channel.id)

        self.current = song
        self._requestor_id = requestor_id
        await self.play(song)

    async def enqueue(self, user, track):
        """Adds a single track to the queue from a given user."""
        await self.queue.put((user.id, track))
        if not self.is_playing:
            await self.play_next()

    async def remove_entry(self, user, idx):
        """Removes a track from the player queue.

        If the provided user is not the one who requested the track. Raises
        Unauthorized.
        If the index is invalid, raises IndexError
        """
        user_id, track = self.queue[idx]
        if user.id != user_id:
            raise Unauthorized
        res = self.queue.remove(idx)
        assert res == (user_id, track)
        if len(self.queue) <= 0:
            await self.play_next()
        return res

    async def clear_user(self, user):
        """Removes all of the user's enqueued tracks from the queue"""
        count = self.queue.remove_all(user.id)
        if len(self.queue) <= 0:
            await self.play_next()
        return count

    def shuffle_user(self, user):
        """Shuffles all of the user's enqueued tracks from the queue"""
        return self.queue.shuffle(user.id)

    async def vote_to_skip(self, user, threshold):
        """Adds a vote to skip the current song. If over the threshold it will
        skip the current song.

        Returns true if the song was skipped, false otherwise.
        """
        self.skip_votes.add(user.id)
        if user.id == self._requestor_id or len(self.skip_votes) >= threshold:
            await self.play_next()
            return True
        return False

    def clear_vote(self, user):
        """Removes a vote to skip from the vote pool."""
        self.skip_votes.remove(user.id)

    async def stop(self):
        """Clears the queue and disconnects the voice client."""
        self.queue.clear()
        await self.play_next()
        await asyncio.gather(*[ui_msg.stop() for ui_msg in self.ui_msgs])
        self.ui_msgs.clear()

    async def set_volume(self, volume):
        await super().set_volume(volume)

        def set_volume(cfg):
            cfg.volume = volume
        await self.guild_proxy.edit_config('music', set_volume)

    async def create_queue_message(self, channel):
        return await self.__run_ui(MusicQueueUI, channel)

    async def create_now_playing_message(self, channel):
        return await self.__run_ui(MusicNowPlayingUI, channel)

    async def __run_ui(self, ui_type, channel):
        for ui_msg in list(self.ui_msgs):
            if isinstance(ui_msg, ui_type):
                await ui_msg.stop()
                self.ui_msgs.remove(ui_msg)
        ui = ui_type(self)
        self.ui_msgs.append(ui)
        return await ui.run(channel)


class MusicPlayerUI(embed.MessageUI):

    def __init__(self, player):
        assert player is not None
        super().__init__(player.bot)
        self.player = player


class MusicNowPlayingUI(MusicPlayerUI):

    async def create_content(self):
        channel = self.player.voice_channel
        if channel is None or self.player.current is None:
            await self.stop()  # Nothing playing, stop here
            return ':notes: **Now Playing...**'
        return f':notes: **Now Playing in {channel.name}...**'

    async def create_embed(self):
        ui_embed = discord.Embed()
        track = self.player.current
        if track is None:
            ui_embed.title = 'No music playing'
            prefix = utils.STOP_EMOJI
            suffix = ''
            progress = 2.0  # At 200% progress, blank progress bar
            await self.stop()
        else:
            ui_embed.title = track.title
            ui_embed.url = track.uri
            prefix = (utils.PAUSE_EMOJI if self.player.paused
                      else utils.PLAY_EMOJI)
            suffix = (f'`[{utils.time_format(self.player.position)}/'
                      f'{utils.time_format(track.duration)}]`')
            progress = float(self.player.position) / float(track.duration)

        requestor = await self.player.get_current_requestor()
        if requestor is not None:
            avatar_url = (requestor.avatar_url or
                          requestor.default_avatar_url)
            name = f'{requestor.name}#{requestor.discriminator}'
            ui_embed.set_author(name=name, icon_url=avatar_url)

        progress_bar = utils.progress_bar(progress, PROGRESS_BAR_WIDTH)
        ui_embed.description = prefix + progress_bar + suffix + ":speaker:"
        return ui_embed


class MusicQueueUI(MusicNowPlayingUI):

    def __init__(self, player):
        super().__init__(player)
        self.current_page = 0
        self.frozen_queue = list(self.player.queue)
        self.iterations_left = 60   # 5 minutes

        self.add_button(utils.PREV_PAGE_EMOJI, self.__next_page)
        self.add_button(utils.NEXT_PAGE_EMOJI, self.__prev_page)

    def __prev_page(self):
        self.current_page = max(self.current_page - 1, 0)

    def __next_page(self):
        queue_length = len(self.player.queue)
        page_count = math.ceil(queue_length / TRACKS_PER_PAGE)
        self.current_page = min(self.current_page + 1, page_count - 1)

    async def create_content(self):
        track = self.player.current
        if track is None or len(self.player.queue) <= 0:
            return await super().create_content()
        duration = utils.time_format(self.get_queue_duration())
        return (f'{utils.PLAY_EMOJI} **{track.title}**\n :notes: Current Queue'
                f' | {len(self.player.queue)} entries | `{duration}`')

    async def create_embed(self):
        ui_embed = discord.Embed()
        queue_length = len(self.player.queue)
        if queue_length <= 0:
            return await super().create_embed()
        queue = list(self.player.queue)
        elem = []
        page_start = TRACKS_PER_PAGE * self.current_page
        page_end = page_start + TRACKS_PER_PAGE
        page_count = math.ceil(queue_length / TRACKS_PER_PAGE)
        idx = page_start + 1
        for requestor_id, track in queue[page_start:page_end]:
            duration = utils.time_format(track.duration)
            elem.append(f'`{idx}.` `[{duration}]` **{track.title}** - '
                        f'<@{requestor_id}>')
            idx += 1
        ui_embed.description = '\n'.join(elem)
        if page_count > 1:
            ui_embed.set_footer(text=f'Page {self.current_page + 1}/{page_count}')
        self.iterations_left -= 1
        if self.iterations_left <= 0:
            await self.stop()
        return ui_embed

    def get_queue_duration(self):
        return sum(t.duration for r, t in self.frozen_queue)
