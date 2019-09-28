import discord
from hourai.db import models


class NotFoundError(Exception):
    pass


class GuildProxy():

    def __init__(self, guild: discord.Guild, session):
        self.guild = guild
        self.session = session

        self._logging_config = None
        self._validation_config = None

    def save(self):
        """
        Adds all associated database models to the associated session.
        Does not commit the results.
        """
        guild_models = (self._logging_config, self._validation_config)
        for model in guild_models:
            if model is not None:
                self.session.add(model)

    @property
    def logging_config(self):
        if self._logging_config is None:
            self._logging_config = self._get_or_create_model(
                models.LoggingConfig)
        return self._logging_config

    @logging_config.setter
    def set_logging_config(self, cfg):
        assert isinstance(cfg, models.LoggingConfig)
        assert cfg.guild_id == self.guild.id
        self._logging_config = cfg

    @property
    def validation_config(self):
        if self._validation_config is None:
            self._validation_config = self._get_or_create_model(
                models.GuildValidationConfig)
        return self._validation_config

    @validation_config.setter
    def set_validation_config(self, cfg):
        assert isinstance(cfg, models.GuildValidationConfig)
        assert cfg.guild_id == self.guild.id
        self._validation_config = cfg

    def get_modlog_channel(self):
        channel_id = self.logging_config.modlog_channel_id
        if channel_id is None:
            return None
        channel = self.guild.get_channel(channel_id)
        if channel is None:
            raise NotFoundError('Modlog channel not found!')
        return channel

    def set_modlog_channel(self, channel):
        """
        Sets the modlog channel to a certain channel. If channel is none, it
        clears it from the config.
        """
        assert channel is None or channel.guild == self.guild
        channel_id = None if channel is None else channel.id
        self.logging_config.modlog_channel_id = channel_id

    async def send_modlog_message(self, *args, **kwargs):
        """
        Sends a message in the modlog channel of the guild. If the message
        fails, a DM to the server owner is sent.
        """
        try:
            modlog = self.get_modlog_channel()
            if modlog is not None:
                return await modlog.send(*args, **kwargs)
        except discord.Forbidden:
            content = ("Oops! A message for the modlog in `{}` failed to "
                       "send! Please make sure the bot can write to a modlog "
                       "channel properly!")
            content = content.format(self.guild.name)
            await self.guild.owner.send(content)
            return None

    def _get_model(self, model):
        return self.session.query(model).get(self.guild.id)

    def _create_model(self, model, **kwargs):
        kwargs['guild_id'] = self.guild.id
        created_model = model(**kwargs)
        self.session.add(created_model)
        return created_model

    def _get_or_create_model(self, model, **kwargs):
        return self._get_model(model) or self._create_model(model, **kwargs)
