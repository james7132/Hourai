import discord
from hourai.db import proto
from hourai.utils.fake import FakeContextManager


class ModlogMessageable():

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


DEFAULT_TYPES = {
    'logging': proto.LoggingConfig,
    'validation': proto.ValidationConfig,
    'auto': proto.AutoConfig,
    'moderation': proto.ModerationConfig,
    'music': proto.MusicConfig,
    'announce': proto.AnnouncementConfig,
    'role': proto.RoleConfig,
}


class GuildProxy:

    def __init__(self, bot, guild):
        self.bot = bot
        self.guild = guild

        self._config_cache = {}

    @property
    def storage(self):
        return self.bot.storage

    async def get_config(self, name):
        name = name.lower()
        if name not in self._config_cache:
            cache = getattr(self.storage, name + '_configs')
            conf = await cache.get(self.guild.id)
            self._config_cache[name] = conf or DEFAULT_TYPES[name]()
        return self._config_cache[name]

    async def set_config(self, name, cfg):
        name = name.lower()
        cache = getattr(self.storage, name + '_configs')
        await cache.set(self.guild.id, cfg)
        self._config_cache[name] = cfg

    async def edit_config(self, name, edit_func):
        assert edit_func is not None
        conf = await self.get_config(name)
        edit_func(conf)
        await self.set_config(name, conf)

    async def get_modlog(self):
        """Creates a discord.abc.Messageable compatible object corresponding to
        the server's modlog.
        """
        return ModlogMessageable(self.guild, await self.get_config('logging'))

    async def get_validation_role(self):
        config = await self.get_config('validation')
        if config is None or not config.HasField('role_id'):
            return None
        return self.guild.get_role(config.role_id)

    async def get_modlog_channel(self):
        config = await self.get_config('logging')
        if config is None or not config.HasField('modlog_channel_id'):
            return None
        return self.guild.get_channel(config.modlog_channel_id)

    async def set_modlog_channel(self, channel):
        """
        Sets the modlog channel to a certain channel. If channel is none, it
        clears it from the config.
        """
        assert channel is None or channel.guild == self.guild
        config = await self.get_config('logging')
        config = config or proto.LoggingConfig()
        if channel is None:
            config.ClearField('modlog_channel_id')
        else:
            config.modlog_channel_id = channel.id
        await self.set_config('logging', config)
