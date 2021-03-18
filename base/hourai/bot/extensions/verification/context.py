import discord
import logging
import collections
from datetime import datetime
from hourai import utils
from hourai.utils import embed, format
from hourai.db import models

log = logging.getLogger('hourai.verification')
Username = collections.namedtuple('Username', 'name discriminator timestamp')


class VerificationContext:

    def __init__(self, bot, member, guild_config):
        assert member is not None

        self.bot = bot
        self.member = member

        self.approved = True
        self.approval_reasons = []
        self.rejection_reasons = []

        self._usernames = None

    @property
    def guild(self):
        return self.member.guild

    @property
    def config(self):
        return self.guild.config.verification

    @property
    def role(self):
        return self.guild.verification_role

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
            return None, False
        cache = self.guild.invites
        invites = await cache.fetch()
        diff = cache.diff(invites)
        cache.update(invites)
        if len(diff) != 1:
            return None, False
        return diff[0], diff[0] == cache.vanity_invite

    async def apply_role(self):
        if self.approved and self.role and self.role not in self.member.roles:
            try:
                await self.member.add_roles(self.role)
            except discord.Forbidden:
                await self.guild.modlog.send(
                    f'Verified {self.member.mention}, but bot is missing '
                    f' permissions to give them the role')

    async def verify_member(self, verifiers):
        for verifier in verifiers:
            try:
                await verifier.verify_member(self)
            except Exception as error:
                # TODO(james7132) Handle the error
                self.bot.dispatch('log_error', 'Verification', error)
        return self.approved

    async def send_modlog_message(self):
        """Sends verification log to a the guild's modlog."""
        mention = None
        # Only ping a mod if enabled and failed approval
        if self.config.ping_moderator_on_fail and not self.approved:
            _, mention = await utils.mention_random_online_mod(
                    self.bot, self.guild)
        return await self.send_log_message(
            self.guild.modlog, ping_target=mention)

    async def send_log_message(self, messageable, ping_target='',
                               include_invite=True):
        """Sends verification log to a given messagable target.

        messageable must be an appropriate discord.abc.Messageable object.
        ping_target if specified be prepended to message.
        """
        member = self.member
        message = []
        if self.approved:
            message.append(f"Verified user: {member.mention} ({member.id}).")
        else:
            message.append(f"{ping_target}. User {member.mention} "
                           f"({member.id}) requires manual verification.")

        if include_invite:
            invite, vanity = await self.get_join_invite()
            if invite is not None:
                inviter = ''
                if vanity:
                    inviter = "vanity URL"
                elif invite.inviter is not None:
                    inviter = str(invite.inviter)
                if inviter:
                    inviter = f"**{inviter}** using"
                message.append(
                    f"Joined via {inviter} invite **{invite.code}** "
                    f"(**{invite.uses}** uses)")

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
                embed=embed.make_whois_embed(ctx, member))
