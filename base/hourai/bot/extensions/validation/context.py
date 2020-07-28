import discord
import logging
import collections
from datetime import datetime
from hourai import utils
from hourai.utils import embed, format
from hourai.db import models

log = logging.getLogger('hourai.validation')
Username = collections.namedtuple('Username', 'name discriminator timestamp')


class ValidationContext:

    def __init__(self, bot, member, guild_config):
        assert member is not None

        self.bot = bot
        self.member = member
        self.guild_config = guild_config
        self.role = None
        if guild_config.role_id:
            self.role = member.guild.get_role(guild_config.role_id)

        self.approved = True
        self.approval_reasons = []
        self.rejection_reasons = []

        self._usernames = None

    @property
    def guild(self):
        return self.member.guild

    @property
    def guild_proxy(self):
        return self.bot.get_guild_proxy(self.guild)

    @property
    def usernames(self):
        if self._usernames is None:
            names = set()
            if self.member.name is not None:
                names.add(Username(
                    name=self.member.name,
                    discriminator=self.member.discriminator,
                    timestamp=datetime.utcnow()))
            with self.bot.create_storage_session() as session:
                usernames = session.query(models.Username) \
                                   .filter_by(user_id=self.member.id) \
                                   .all()
                names.update([Username(name=u.name,
                                       discriminator=u.discriminator,
                                       timestamp=u.timestamp)
                              for u in usernames])
            self._usernames = names
        return self._usernames

    def add_approval_reason(self, reason):
        assert reason is not None
        if reason not in self.approval_reasons:
            self.approval_reasons.append(reason)
        self.approved = True

    def add_rejection_reason(self, reason):
        assert reason is not None
        if reason not in self.rejection_reasons:
            self.rejection_reasons.append(reason)
        self.approved = False

    async def get_join_invite(self):
        if not self.guild.me.guild_permissions.manage_guild:
            return None
        cache = self.guild_proxy.invites
        invites = await cache.fetch()
        diff = cache.diff(invites)
        cache.update(invites)
        if len(diff) != 1:
            return None
        return diff[0]

    async def apply_role(self):
        if self.approved and self.role and self.role not in self.member.roles:
            try:
                await self.member.add_roles(self.role)
            except discord.Forbidden:
                modlog = await self.guild_proxy.get_modlog()
                await modlog.send(
                    f'Verified {self.member.mention}, but bot is missing '
                    f' permissions to give them the role')

    async def validate_member(self, validators):
        for validator in validators:
            try:
                await validator.validate_member(self)
            except Exception as error:
                # TODO(james7132) Handle the error
                self.bot.dispatch('log_error', 'Validation', error)
        return self.approved

    async def send_modlog_message(self):
        """Sends verification log to a the guild's modlog."""
        modlog = await self.guild_proxy.get_modlog()
        online_mod, mention = utils.mention_random_online_mod(self.guild)
        return await self.send_log_message(
            modlog, ping_target=mention, allowed_mentions=[online_mod])

    async def send_log_message(self, messageable, ping_target=None,
                               allowed_mentions=False, include_invite=True):
        """Sends verification log to a given messagable target.

        messageable must be an appropriate discord.abc.Messageable object.
        ping_target if specified be prepended to message.
        """
        member = self.member
        message = []
        if self.approved:
            message.append(f"Verified user: {member.mention} ({member.id}).")
        elif ping_target is not None:
            message.append(f"{ping_target}. "
                           f"User {member.mention} ({member.id}) requires "
                           f"manual verification.")
        else:
            message.append(f"User {member.mention} ({member.id}) requires "
                           f"manual verification.")

        if include_invite:
            invite = await self.get_join_invite()
            if invite is not None:
                inviter = invite.inviter or "vanity URL"
                message.append(
                    f"Joined via **{inviter}** using invite "
                    f"**{invite.code}** (**{invite.uses}** uses)")

        if len(self.approval_reasons) > 0:
            message += [
                "Approved for the following reasons:",
                f"```{format.bullet_list(self.approval_reasons)}```"
            ]
        if len(self.rejection_reasons) > 0:
            message += [
                "Rejected for the following reasons:",
                f"```{format.bullet_list(self.rejection_reasons)}```"
            ]


        ctx = await self.bot.get_automated_context(content='', author=member)
        async with ctx:
            return await messageable.send(
                content="\n".join(message),
                embed= embed.make_whois_embed(ctx, member))
