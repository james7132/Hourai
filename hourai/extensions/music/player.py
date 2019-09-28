import asyncio
import logging
import wavelink
from .queue import MusicQueue

log = logging.getLogger(__name__)


def _get_voice_channel(guild):
    channels = filter(lambda ch: ch.permissions_for(guild.me).connect,
                      guild.voice_channesls)
    return next(channels, None)


class Player(wavelink.Player):

    def __init__(self, bot, guild_id: int, node: wavelink.Node):
        super().__init__(bot, guild_id, node)

        self.next_event = asyncio.Event()
        self.queue = MusicQueue()

        self.volume = 40
        self.current = None

        self.skip_votes = set()

        bot.loop.create_task(self.player_loop())

    async def player_loop(self):
        await self.bot.wait_until_ready()

        await self.set_preq('Flat')
        # We can do any pre loop prep here...
        await self.set_volume(self.volume)

        while True:
            self.next_event.clear()

            if self.is_connected and len(self.queue) <= 0:
                await self.disconnect()

            user, song = await self.queue.get()
            if not song:
                continue

            if not self.is_connected:
                channel = _get_voice_channel(self.guild)
                if channel is None:
                    continue
                await self.connect(channel.id)

            self.current = song
            self.paused = False

            await self.play(song)

            # Wait for TrackEnd event to set our event...
            await self.next_event.wait()

            # Clear votes...
            self.skip_votes.clear()

    @property
    def guild(self):
        return self.bot.get_guild(self.guild_id)

    @property
    def voice_client(self):
        return self.guild.voice_client if self.guild is not None else None

    @property
    def channel(self):
        return self.voice_client.channel if self.guild is not None else None

    @property
    def entries(self):
        return list(self.queue)

    def play_next(self):
        """Plays the next song. If a song is currently playing, it will be
        skipped.
        """
        self.next_event.set()

    def enqueue(self, user, track):
        """Adds a single track to the queue from a given user."""
        self.queue.put((user.id, track))

    def clear_user(self, user):
        """Removes all of the user's enqueued tracks from the queue"""
        return self.queue.remove_all(user.id)

    def shuffle_user(self, user):
        """Shuffles all of the user's enqueued tracks from the queue"""
        self.queue.remove_all(user.id)

    def vote_to_skip(self, user, threshold):
        """Adds a vote to skip the current song. If over the threshold it will
        skip the current song.

        Returns true if the song was skipped, false otherwise.
        """
        self.skip_votes.add(user.id)
        # TODO(james7132): Allow user to force skip their own song.
        if len(self.skip_votes) > threshold:
            self.play_next()
            return True
        return False

    def clear_vote(self, user):
        """Removes a vote to skip from the vote pool."""
        self.skip_votes.remove(user.id)

    def stop(self):
        """Clears the queue and disconnects the voice client."""
        self.queue.clear()
        self.play_next()
