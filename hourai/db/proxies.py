import discord
from hourai.db import proto
from hourai.utils.fake import FakeContextManager


class ModlogMessageable(discord.abc.Messageable):

    def __init__(self, guild, config):
        assert guild is not None
        self.guild = guild
        self.config = config

    async def send(self, *args, **kwargs):
        try:
            modlog = self.__get_modlog_channel()
            if modlog is not None:
                return await modlog.send(*args, **kwargs)
        except discord.Forbidden:
            content = ("Oops! A message for the modlog in `{}` failed to "
                       "send! Please make sure the bot can write to a modlog "
                       "channel properly!")
            content = content.format(self.guild.name)
            await self.guild.owner.send(content)
            return None

    def typing(self):
        modlog = self.__get_modlog_channel()
        return modlog.typing() if modlog is not None else FakeContextManager()

    async def trigger_typing(self):
        modlog = self.__get_modlog_channel()
        if modlog is not None:
            await modlog.trigger_typing()

    async def fetch_message(self, id):
        modlog = self.__get_modlog_channel()
        if modlog is not None:
            return await modlog.fetch_message(id)
        else:
            raise discord.NotFound()

    async def pins(self):
        modlog = self.__get_modlog_channel()
        return [] if modlog is None else await modlog.pins()

    def history(self):
        # TODO(james7132): Likely won't be called, but this will fail if no
        # modlog is found or set. Fix this
        return self.__get_modlog_channel().history()

    def __get_modlog_channel(self):
        if self.config is None or not self.config.HasField('modlog_channel_id'):
            return None
        return self.guild.get_channel(self.config.modlog_channel_id)


class GuildProxy:

    def __init__(self, bot, guild):
        self.bot = bot
        self.guild = guild

        self._logging_config = None
        self._validation_config = None

    @property
    def storage(self):
        return self.bot.storage

    @property
    def modlog(self):
        """Creates a discord.abc.Messageable compatible object corresponding to
        the server's modlog.
        """
        return ModlogMessageable(self.guild, self.get_logging_config)

    async def get_logging_config(self):
        if self._logging_config is None:
            self._logging_config = await self.storage.logging_configs.get(
                    self.guild.id)
        return self._logging_config

    async def set_logging_config(self, cfg):
        await self.storage.logging_configs.set(self.guild.id, cfg)
        self._logging_config = cfg

    async def get_validation_config(self):
        if self._validation_config is None:
            self._validation_config = (
                await self.storage.validation_configs.get(self.guild.id))
        return self._validation_config

    async def set_validation_config(self, cfg):
        await self.storage.validation_configs.set(self.guild.id, cfg)
        self._validation_config = cfg

    async def get_modlog_channel(self):
        config = await self.get_logging_config()
        if config is None or not config.HasField('modlog_channel_id'):
            return None
        return self.guild.get_channel(config.modlog_channel_id)

    async def set_modlog_channel(self, channel):
        """
        Sets the modlog channel to a certain channel. If channel is none, it
        clears it from the config.
        """
        assert channel is None or channel.guild == self.guild
        config = await self.get_logging_config()
        config = config or proto.LoggingConfig()
        if channel is None:
            config.ClearField('modlog_channel_id')
        else:
            config.modlog_channel_id = channel.id
        await self.set_logging_config(config)
