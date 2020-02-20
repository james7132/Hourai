import logging
from hourai import utils
from hourai.utils import embed
from hourai.db import proxies

log = logging.getLogger('hourai.validation')


class ValidationContext():

    def __init__(self, bot, member, guild_config):
        assert member is not None

        self.bot = bot
        self.member = member
        self.guild_config = guild_config
        self.role = None
        if guild_config.validation_role_id:
            self.role = member.guild.get_role(guild_config.validation_role_id)

        self.approved = True
        self.approval_reasons = []
        self.rejection_reasons = []

    @property
    def guild(self):
        return self.member.guild

    @property
    def guild_proxy(self):
        return proxies.GuildProxy(self.bot, self.member.guild)

    def usernames(self):
        names = set()
        if self.member.name is not None:
            names.add(self.member.name)
        return names

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
            except Exception:
                # TODO(james7132) Handle the error
                log.exception('Error while running validator:')
        return self.approved

    async def send_modlog_message(self):
        member = self.member
        if self.approved:
            message = f"Verified user: {member.mention} ({member.id})."
        else:
            message = (f"{utils.mention_random_online_mod(member.guild)}. "
                       f"User {member.name} ({member.id}) requires manual "
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
            return await self.guild_proxy.send_modlog_message(
                    content=message, embed=whois_embed)
