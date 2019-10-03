import discord
from hourai.db import proto


class GuildProxy:

    def __init__(self, bot, guild):
        self.bot = bot
        self.guild = guild

        self._logging_config = None
        self._validation_config = None

    @property
    def storage(self):
        return self.bot.storage

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
            self._validation_config = await self.storage.validation_configs.get(
                    self.guild.id)
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

    async def send_modlog_message(self, *args, **kwargs):
        """
        Sends a message in the modlog channel of the guild. If the message
        fails, a DM to the server owner is sent.
        """
        try:
            modlog = await self.get_modlog_channel()
            if modlog is not None:
                return await modlog.send(*args, **kwargs)
        except discord.Forbidden:
            content = ("Oops! A message for the modlog in `{}` failed to "
                       "send! Please make sure the bot can write to a modlog "
                       "channel properly!")
            content = content.format(self.guild.name)
            await self.guild.owner.send(content)
            return None
