import discord
from datetime import datetime, timezone
from typing import List
from discord import flags
from hourai import utils
from hourai.db import proto
from hourai.utils.fake import FakeContextManager


@flags.fill_with_flags()
class Permissions(flags.BaseFlags):

    def __init__(self, permissions=0, **kwargs):
        if not isinstance(permissions, int):
            raise TypeError('Expected int parameter, received %s instead.' %
                            permissions.__class__.__name__)

        self.value = permissions
        super().__init__(**kwargs)

    @flags.flag_value
    def self_serve(self) -> int:
        return 1 << 0

    @flags.flag_value
    def is_dj(self) -> int:
        return 1 << 1

    @flags.flag_value
    def moderator(self) -> int:
        return 1 << 2


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
        if self.config is None or \
                not self.config.HasField('modlog_channel_id'):
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


class InviteCache:

    """An in-memory cache of the invites for a given guild"""
    __slots__ = ('guild', '_cache', 'vanity_invite')

    def __init__(self, guild):
        assert guild is not None
        self.guild = guild
        self.vanity_invite = None
        self._cache = {}

    async def fetch(self) -> dict:
        """Fetches the remote state of all invites in the guild. This includes
        the vanity invite, if available.
        """
        if not self.guild.me.guild_permissions.manage_guild:
            return {}

        invites = await self.guild.invites()

        try:
            if "VANITY_URL" in self.guild.features:
                self.vanity_invite = await self.guild.vanity_invite()
                invites.append(self.vanity_invite)
            else:
                self.vanity_invite = None
        except discord.NotFound:
            # Sometimes VANITY_URL is set but there is no invite
            self.vanity_invite = None

        return {inv.code: inv for inv in invites}

    def diff(self, updated: dict) -> list:
        """Diffs the internal state of the cache and pulls out the differing
        elements.
        """
        keys = set(self._cache.keys()) & set(updated.keys())
        return [updated[k] for k in keys
                if self._cache[k].uses != updated[k].uses]

    def update(self, values: dict) -> None:
        """Updates the cache with a dict of values."""
        self._cache = values

    async def refresh(self) -> None:
        """Fetches the remote state of all invites in the guild and updates the
        cache with the results.

        Shorthand for update(await fetch()).
        """
        self.update(await self.fetch())

    def add(self, invite: discord.Invite) -> None:
        """Adds a new invite to the cache."""
        self._cache[invite.code] = invite

    def remove(self, invite: discord.Invite) -> None:
        """Removes an invite to the cache."""
        try:
            del self._cache[invite.code]
        except KeyError:
            pass


class ConfigCache:

    __slots__ = ('storage', 'guild', '_cache')

    def __init__(self, storage, guild):
        self.storage = storage
        self.guild = guild
        self._cache = {}

    async def get(self, name):
        name = name.lower()
        if name not in self._cache:
            cache = getattr(self.storage, name + '_configs')
            conf = await cache.get(self.guild.id)
            self._cache[name] = conf or DEFAULT_TYPES[name]()
        return self._cache[name]

    async def set(self, name, cfg):
        name = name.lower()
        cache = getattr(self.storage, name + '_configs')
        await cache.set(self.guild.id, cfg)
        self._cache[name] = cfg


class GuildProxy:

    __slots__ = ('bot', 'guild', 'config', 'invites')

    def __init__(self, bot, guild):
        self.bot = bot
        self.guild = guild

        self.config = ConfigCache(self.storage, guild)

        # Ephemeral state associated with a Discord guild. Lost on bot restart.
        self.invites = InviteCache(guild)

    @property
    def storage(self):
        return self.bot.storage

    async def is_locked_down(self):
        config = await self.config.get('validation')
        if not config.HasField('lockdown_expiration'):
            return False
        expiration = datetime.fromtimestamp(config.lockdown_expiration)
        return datetime.utcnow() <= expiration

    async def set_lockdown(self, expiration=datetime.max):
        config = await self.config.get('validation')
        config.lockdown_expiration = \
                int(expiration.replace(tzinfo=timezone.utc).timestamp())
        await self.config.set('validation', config)

    async def clear_lockdown(self):
        config = await self.config.get('validation')
        config.ClearField('lockdown_expiration')
        await self.config.set('validation', config)

    async def get_role_permissions(self, role: discord.Role) -> Permissions:
        config = await self.config.get('role')
        return Permissions(config.settings[role.id].permissions)

    async def get_member_permissions(
            self, member: discord.Member) -> Permissions:
        config = await self.config.get('role')

        permissions = 0
        for role_id in member._roles:
            permissions = config.settings[role_id].permissions
        return Permissions(permissions)

    async def find_moderators(self) -> List[discord.Member]:
        config = await self.config.get('role')

        roles = self.guild.roles
        perms = (Permissions(config.settings[role.id].permissions)
                 for role in roles)
        mod_roles = [role for role, perm in zip(roles, perms)
                     if perm.moderator]
        if not mod_roles:
            return utils.find_moderator(self.guild)

        return list(utils.all_with_roles(self.guild.members, mod_roles))

    async def get_modlog(self):
        """Creates a discord.abc.Messageable compatible object corresponding to
        the server's modlog.
        """
        return ModlogMessageable(self.guild, await self.config.get('logging'))

    async def get_validation_role(self):
        config = await self.config.get('validation')
        if config is None or not config.HasField('role_id'):
            return None
        return self.guild.get_role(config.role_id)

    async def get_modlog_channel(self):
        config = await self.config.get('logging')
        if config is None or not config.HasField('modlog_channel_id'):
            return None
        return self.guild.get_channel(config.modlog_channel_id)

    async def set_modlog_channel(self, channel):
        """Sets the modlog channel to a certain channel. If channel is none, it
        clears it from the config.
        """
        assert channel is None or channel.guild == self.guild
        config = await self.config.get('logging')
        if channel is None:
            config.ClearField('modlog_channel_id')
        else:
            config.modlog_channel_id = channel.id
        await self.config.set('logging', config)
