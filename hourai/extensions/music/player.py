import discord
import asyncio
import logging
import wavelink
import math
from .queue import MusicQueue
from . import utils
from hourai.utils import embed
from hourai.db import proto

log = logging.getLogger('hourai.music.player')


PROGRESS_BAR_WIDTH = 12
TRACKS_PER_PAGE = 10
POST_TRACK_WAIT = 5.0


class Unauthorized(Exception):
    pass


class HouraiMusicPlayer(wavelink.Player):

    def __init__(self, bot, guild_id: int, node: wavelink.Node):
        super().__init__(bot, guild_id, node)

        self.next_event = asyncio.Event()
        self.queue = MusicQueue()

        self.current = None
        self._requestor_id = None

        self.skip_votes = set()

        self.queue_msg = None
        self.now_playing_msg = None

        self.queue_ui = None
        self.np_msg = None

        bot.loop.create_task(self.__player_loop())

    async def get_music_config(self):
        config = await self.bot.storage.music_configs.get(self.guild_id)
        return config or proto.MusicConfig()

    async def set_music_config(self, music_config):
        await self.bot.storage.music_configs.set(self.guild_id,
                                                 music_config)

    def get_default_channel(self):
        return discord.utils.find(
                lambda ch: ch.permissions_for(self.guild.me).connect,
                self.guild.voice_channels)

    async def get_voice_channel(self):
        music_config = await self.get_music_config()
        channel = None
        if music_config.HasField('voice_channel_id'):
            channel = self.guild.get_channel(music_config.voice_channel_id)
            if isinstance(channel, discord.VoiceChannel):
                channel = None
        return channel or self.get_default_channel()

    async def __init_player_loop(self):
        await self.set_preq('Flat')
        config = await self.get_music_config()
        await self.set_volume(config.volume)

    async def __player_loop(self):
        await self.bot.wait_until_ready()
        await self.__init_player_loop()
        while True:
            try:
                self.next_event.clear()

                if self.is_connected and len(self.queue) <= 0:
                    # Stop playing the song and disconnect
                    await self.disconnect()
                    await super().stop()

                requestor_id, song = await self.queue.get()
                if not song:
                    continue

                if not self.is_connected:
                    channel = await self.get_voice_channel()
                    if channel is None:
                        continue
                    await self.connect(channel.id)

                self.current = song
                self._requestor_id = requestor_id

                await self.play(song)

                self.next_event.clear()
                # Wait for TrackEnd event to set our event...
                await self.next_event.wait()

                # Clear votes...
                self.skip_votes.clear()
                self.current = None
                self._requestor_id = None
            except Exception:
                log.exception(f'Exception while playing for guild '
                              f'{self.guild_id}:')

    async def disconnect(self):
        await super().disconnect()
        self.current = None
        self._requestor_id = None
        self.skip_votes.clear()

    async def hook(self, event):
        if isinstance(event, wavelink.TrackEnd):
            if event.reason in ('FINISHED', 'LOAD_FAILED'):
                self.play_next()
            log.error(event.error)

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

    @property
    def current_requestor(self):
        guild = self.guild
        if self._requestor_id is None or guild is None:
            return None
        return guild.get_member(self._requestor_id)

    @property
    def is_playing(self):
        return self.is_connected and self.current is not None

    def play_next(self, skip=False):
        """Plays the next song. If a song is currently playing, it will be
        skipped.
        """
        # Avoid triggering the
        if self.is_playing:
            self.next_event.set()

    def enqueue(self, user, track):
        """Adds a single track to the queue from a given user."""
        return self.queue.put((user.id, track))

    def remove_entry(self, user, idx):
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
        return res

    def clear_user(self, user):
        """Removes all of the user's enqueued tracks from the queue"""
        return self.queue.remove_all(user.id)

    def shuffle_user(self, user):
        """Shuffles all of the user's enqueued tracks from the queue"""
        return self.queue.shuffle(user.id)

    def vote_to_skip(self, user, threshold):
        """Adds a vote to skip the current song. If over the threshold it will
        skip the current song.

        Returns true if the song was skipped, false otherwise.
        """
        self.skip_votes.add(user.id)
        if self.current_requestor == user or len(self.skip_votes) > threshold:
            self.play_next(skip=True)
            return True
        return False

    def clear_vote(self, user):
        """Removes a vote to skip from the vote pool."""
        self.skip_votes.remove(user.id)

    async def stop(self):
        """Clears the queue and disconnects the voice client."""
        self.queue.clear()
        self.play_next()
        if self.queue_msg is not None:
            await self.queue_msg.stop()
        if self.np_msg is not None:
            await self.np_msg.stop()

    async def set_volume(self, volume):
        await super().set_volume(volume)
        music_config = (await self.get_music_config()) or proto.MusicConfig()
        music_config.volume = volume
        await self.set_music_config(music_config)

    async def create_queue_message(self, channel):
        return await self.__run_ui('queue_msg', MusicQueueUI, channel)

    async def create_now_playing_message(self, channel):
        return await self.__run_ui('np_msg', MusicNowPlayingUI, channel)

    async def __run_ui(self, attr, ui_type, channel):
        ui = getattr(self, attr)
        if ui is not None:
            await ui.stop()
        ui = ui_type(self)
        setattr(self, attr, ui)
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
        embed = discord.Embed()
        track = self.player.current
        if track is None:
            embed.title = 'No music playing'
            prefix = utils.STOP_EMOJI
            suffix = ''
            progress = 2.0  # At 200% progress, blank progress bar
            await self.stop()
        else:
            embed.title = track.title
            embed.url = track.uri
            prefix = (utils.PAUSE_EMOJI if self.player.paused
                      else utils.PLAY_EMOJI)
            suffix = (f'`[{utils.time_format(self.player.position)}/'
                      f'{utils.time_format(track.duration)}]`')
            progress = float(self.player.position) / float(track.duration)

        requestor = self.player.current_requestor
        if requestor is not None:
            avatar_url = (requestor.avatar_url or
                          requestor.default_avatar_url)
            name = f'{requestor.name}#{requestor.discriminator}'
            embed.set_author(name=name, icon_url=avatar_url)

        progress_bar = utils.progress_bar(progress, PROGRESS_BAR_WIDTH)
        embed.description = prefix + progress_bar + suffix + ":speaker:"
        return embed


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
        if page_count > 1:
            embed.set_footer(text=f'Page {self.current_page + 1}/{page_count}')
        if page_count > 1:
            embed.set_footer(text=f'Page {self.current_page + 1}/{page_count}')
        self.iterations_left -= 1
        if self.iterations_left <= 0:
            await self.stop()
        return embed

    def get_queue_duration(self):
        return sum(t.duration for r, t in self.frozen_queue)
