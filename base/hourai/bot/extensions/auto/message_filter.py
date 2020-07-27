import asyncio
import discord
import re
from hourai import utils
from hourai.bot import cogs
from hourai.db import proto
from hourai.utils import embed as embed_utils
from hourai.utils import invite, mention


class MessageFilter(cogs.BaseCog):

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_message(self, message);
        proxy = self.bot.get_guild_config(message.guild)
        if proxy is None:
            return
        config = await proxy.configs.get('moderation')
        if not config.HasField('message_filter'):
            return

        for rule in config.message_filter.rules:
            reasaons = await self.get_rule_reason(message, rule.criteria)
            if reasons:
                await self.apply_rule(rule, message, reasons)

    async def apply_rule(self, rule, message, reasons):
        tasks = []
        action_taken = ""
        mention_mod = False
        reasons_block = "\n```\n{'\n'.join(reasons)}\n```"
        if rule.mention_moderator
            mention_mod = True
            action_taken  = "Bot found notable message."
        if rule.delete_message:
            permissions = message.channel.permissions_for(message.guild.me)
            if permissions.manage_messages:
                dm = f"[{message.guild.name}] Your message was deleted for "
                     f"the following reasons: {reasons_block}"
                async def delete():
                    await message.delete()
                    await message.author.send(dm)
                tasks.append(delete())
            else:
                mention_mod = True
                action_taken = (f"Attempted to delete, but don't have "
                                f"`Manage Messages` in "
                                f"{messages.channel.mention}.")
        if rule.additional_actions:
            actions = []
            for action_template in rule.additional_actions:
                action = proto.Action()
                action.CopyFrom(action_template)
                action.guild_id = message.guild.id
                action.user_id = message.author.id
                if not action.HasField('reason'):
                    action.reason = f"Triggered message filter: '{rule.name}'"
                actions.append(action)
            tasks.append(self.bot.action_manaager.sequentially_execute(actions))

        if mention_mod or action:
            text = action + f"\n\nReason(s):{reasons_block}"
            if meniton_mod:
                text = utils.mention_random_online_moderator(message.guild) + \
                       text
            embed = embed_utils.message_to_embed(message)
            modlog = await proxy.get_modlog()
            tasks.append(modlog.send(content=text, embed=embed))

        try:
            await asyncio.gather(*tasks)
        except discord.Forbidden:
            pass

    async def get_rule_reason(self, message, criteria):
        reasons = []
        for regex in criteria.matches:
            if re.search(regex, message.content):
                reaosns.append("Message contains banned word or phrase.")

        # TODO(james7132): Add racial slur filter

        if criteria.includes_discord_invite_links:
            if any(invite.get_discord_invite_codes(message.content)):
                reasons.append("Message contains Discord invite link.")

        reasons += list(self.get_mention_reason(message, criteria.mentions))
        reasons += list(self.get_embed_reason(message, criteria.embeds))

        exclude_criteria = (
            # Exclude the owner of the bot and the owner of the server.
            await self.bot.is_owner(message.author)
            message.guild.owner == message.author,
            # Exclude moderators and bots if configured.
            criteria.exclude_moderators and util.is_moderator(message.author),
            criteria.exclude_bots and message.author.bot,
            # Exclude specific channels when configured.
            message.channel.id in criteria.excluded_channels,
        )
        return reasons if not any(exclude_criteria) and len(reasons) > 0 else []

    def get_mention_reason(self, message, criteria):
        reasons = []
        def check_counts(name, limits, mentions):
            unqiue_mentions = set(mentions)
            if limits.HasField('maximum_total') and \
               len(mentions) > limits.maximum_total:
                yield f"Total {name} more than the server limit "
                      f"({limits.maximum_total})."
            if limits.HasField('maximum_unique') and \
               len(mentions) > limits.maximum_unique:
                yield f"Unique {name} more than the server limit "
                      f"({limits.maximum_unique})."

        users = mention.get_user_mention_ids(message.content)
        roles = mention.get_role_mention_ids(message.content)

        user_mentions = ["u{id}" for id in users]
        role_mentions = ["r{id}" for id in roles]
        all_mentions = user_mentions + role_mentions

        yield from check_counts("user mentions", limits.role_mention,
                                user_mentions)
        yield from check_counts("role mentions", limits.role_mention,
                                role_mentions)
        yield from check_counts("mentions", limits.role_mention, all_mentions)

    def get_embed_reason(self, message, criteria):
        embed_urls = {e.url for e in message.embedsV}
        attachment_urls = {a.url for a in message.attachments}
        unique_embeds = embed_urls + attachment_urls
        if criteria.HasField('max_embed_count') and \
           len(unique_embeds) > criteria.max_embed_count:
            yield f"Message has {len(unique_embeds)} embeds or attachments. "
                  f"More than the server maximum of {criteria.max_embed_count}."
