import discord
from datetime import datetime, timezone
from typing import List
from discord import flags
from hourai import utils
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
        if not self.config.HasField('modlog_channel_id'):
            return None
        return self.guild.get_channel(self.config.modlog_channel_id)


class InviteCache:

    """An in-memory cache of the invites for a given guild"""
    __slots__ = ('_guild', '_cache', 'vanity_invite')

    def __init__(self, guild):
        assert guild is not None
        self._guild = guild
        self.vanity_invite = None
        self._cache = {}

    async def fetch(self) -> dict:
        """Fetches the remote state of all invites in the guild. This includes
        the vanity invite, if available.
        """
        if not self._guild.me.guild_permissions.manage_guild:
            return {}

        invites = await self._guild.invites()

        try:
            if "VANITY_URL" in self._guild.features:
                self.vanity_invite = await self._guild.vanity_invite()
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


class HouraiGuild(discord.Guild):

    __slots__ = ('config', 'invites')

    def __init__(self, *, data, state):
        super().__init__(data=data, state=state)
        self.config = None

        # Ephemeral state associated with a Discord guild. Lost on bot restart.
        self.invites = InviteCache(super())

    @property
    def storage(self):
        return self._state.storage

    @property
    def modlog(self):
        """Creates a discord.abc.Messageable compatible object corresponding to
        the server's modlog.
        """
        return ModlogMessageable(self, self.config.logging)

    @property
    def is_locked_down(self):
        if not self.config.validation.HasField('lockdown_expiration'):
            return False
        expiration = datetime.fromtimestamp(self.config.lockdown_expiration)
        return datetime.utcnow() <= expiration

    @property
    async def validation_role(self):
        if not self.config.validation.HasField('role_id'):
            return None
        return self.get_role(self.config.validation.role_id)

    def should_cache_member(member):
        # Cache if the member is:
        # - A moderator
        # - Is the bot user
        # - Is a member pending verification
        return utils.is_moderator(member) or \
                member.id == member._state.user.id or \
                member.pending

    def _add_member(self, member, force=False):
        if member is not None and force or self.should_cache_member(member):
            super()._add_member(member)

    async def destroy(self):
        await self.storage.guild_configs.clear(self.id)

    async def refresh_config(self):
        self.config = await self.storage.guild_configs.get(self.id)

    async def flush_config(self):
        await self.storage.guild_configs.set(self.id, self.config)

    async def set_lockdown(self, expiration=datetime.max):
        self.config.lockdown_expiration = \
                int(expiration.replace(tzinfo=timezone.utc).timestamp())
        await self.flush_config()

    async def clear_lockdown(self):
        self.config.ClearField('lockdown_expiration')
        await self.flush_config()

    def get_role_permissions(self, role: discord.Role) -> Permissions:
        return Permissions(self.config.role.settings[role.id].permissions)

    def get_member_permissions(
            self, member: discord.Member) -> Permissions:
        permissions = 0
        for role_id in member._roles:
            permissions = self.config.role.settings[role_id].permissions
        return Permissions(permissions)

    def find_moderators(self) -> List[discord.Member]:
        settings = self.config.role.settings
        mod_roles = (self.get_role(role_id)
                     for role_id, setting in settings.items()
                     if Permissions(settings.permissions).moderator)
        mod_roles = [role for role in mod_roles if role is not None]
        if not mod_roles:
            return utils.find_moderator(self)
        return list(utils.all_with_roles(self.members, mod_roles))

    async def set_modlog_channel(self, channel):
        """Sets the modlog channel to a certain channel. If channel is none, it
        clears it from the config.
        """
        assert channel is None or channel.guild.id == self.id
        if channel is None:
            self.config.logging.ClearField('modlog_channel_id')
        else:
            self.config.logging.modlog_channel_id = channel.id
        await self.flush_config()
