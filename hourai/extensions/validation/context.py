import logging
from datetime import datetime
from hourai import utils
from hourai.utils import embed, format
from hourai.db import proxies, models

log = logging.getLogger('hourai.validation')


class ValidationContext():

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
        return proxies.GuildProxy(self.bot, self.member.guild)

    @property
    def usernames(self):
        if self._usernames is None:
            names = set()
            if self.member.name is not None:
                names.add(models.Username(
                    name=self.member.name,
                    discriminator=self.member.discriminator,
                    timestamp=datetime.utcnow()))
            with self.bot.create_storage_session() as session:
                names.update([x for x, in session.query(
                    models.Username).filter_by(user_id=self.member.id)
                                         .distinct()])
            self._usernames = names
        return self._usernames

    def add_approval_reason(self, reason):
        assert reason is not None
        self.approval_reasons.append(reason)
        self.approved = True

    def add_rejection_reason(self, reason):
        assert reason is not None
        self.rejection_reasons.append(reason)
        self.approved = False

    async def apply_role(self):
        if self.approved and self.role and self.role not in self.member.roles:
            await self.member.add_roles(self.role)

    async def validate_member(self, validators):
        for validator in validators:
            try:
                await validator.validate_member(self)
            except Exception as error:
                # TODO(james7132) Handle the error
                log.exception('Error while running validator:')
                await self.bot.send_owner_error(error)
        return self.approved

    async def send_modlog_message(self):
        """Sends verification log to a the guild's modlog."""
        modlog = await self.guild_proxy.get_modlog()
        return await self.send_log_message(
                modlog, utils.mention_random_online_mod(self.guild))

    async def send_log_message(self, messageable, ping_target=None):
        """Sends verification log to a given messagable target.

        messageable must be an appropriate discord.abc.Messageable object.
        ping_target if specified be prepended to message.
        """
        member = self.member
        if self.approved:
            message = f"Verified user: {member.mention} ({member.id})."
        elif ping_target is not None:
            message = (f"{ping_target}. "
                       f"User {member.name} ({member.id}) requires manual "
                       f"verification.")
        else:
            message = (f"User {member.name} ({member.id}) requires manual "
                       f"verification.")

        if len(self.approval_reasons) > 0:
            message += ("\nApproved for the following reasons: \n"
                        f"```\n{format.bullet_list(self.approval_reasons)}\n"
                        f"```")
        if len(self.rejection_reasons) > 0:
            message += (f"\nRejected for the following reasons: \n"
                        f"```\n{format.bullet_list(self.rejection_reasons)}\n"
                        f"```")

        ctx = await self.bot.get_automated_context(content='', author=member)
        async with ctx:
            whois_embed = embed.make_whois_embed(ctx, member)
            return await messageable.send(content=message, embed=whois_embed)
