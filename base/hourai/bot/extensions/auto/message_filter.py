import asyncio
import discord
import re
import logging
from discord.ext import commands
from hourai import utils
from hourai.bot import cogs
from hourai.db import proto
from hourai import config as hourai_config
from hourai.utils import embed as embed_utils
from hourai.utils import invite, mentions, format


def generalize_filter(filter_value):
    filter_value = re.escape(filter_value)

    def _generalize_character(char):
        return char + '+' if char.isalnum() else char
    return ''.join(_generalize_character(char) for char in filter_value)


def make_slur_filter():
    slurs = hourai_config.load_list(hourai_config.get_config(),
                                    'message_filter_slurs')
    components = [generalize_filter(s) for s in slurs]
    regex = f"({'|'.join(components)})"
    logging.info(f"Slur Filter: {regex}")
    return re.compile(regex)


SLUR_FILTER = make_slur_filter()


class MessageFilter(cogs.BaseCog):

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_message(self, message):
        await self.check_message(message)

    @commands.Cog.listener()
    async def on_raw_messsage_edit(self, payload):
        await self.check_message(payload)

    async def check_message(self, message):
        config = await self.get_mod_config(message)
        if config is None:
            return

        for rule in config.message_filter.rules:
            reasons = await self.get_rule_reason(message, rule.criteria)
            if reasons:
                await self.apply_rule(rule, message, reasons)

    async def get_mod_config(self, message):
        proxy = self.bot.get_guild_proxy(message.guild)
        if proxy is None:
            return None

        if isinstance(message, discord.RawMessageUpdateEvent):
            try:
                channel = self.bot.get_channel(message.channel_id)
                message = await channel.fetch_message(message.message_id)
            except (AttributeError, discord.NotFound, discord.Forbidden):
                return

        has_filter = proxy.config.moderation.HasField('message_filter')
        is_bot_user = message.author == self.bot.user
        in_modlog = proxy.config.logging.modlog_channel_id == message.channel.id

        if has_filter and not (in_modlog and is_bot_user):
            return mod_config
        return None

    async def apply_rule(self, rule, message, reasons):
        tasks = []
        action_taken = ""
        mention_mod = rule.notify_moderator
        reasons_block = f"\n```\n{format.vertical_list(reasons)}\n```"
        guild = self.bot.get_guild_proxy(message.guild)

        if rule.notify_moderator:
            action_taken = "Message filter found notable message:"
        if rule.delete_message:
            permissions = message.channel.permissions_for(guild.me)
            if permissions.manage_messages:
                if rule.notify_moderator:
                    action_taken = "Message filter deleted message:"

                dm = (f"[{guild.name}] Your message was deleted for "
                      f"the following reasons: {reasons_block}")

                async def delete():
                    await message.delete()
                    if not message.author.bot and \
                       message.author != self.bot.user:
                        await message.author.send(dm)
                tasks.append(delete())
            else:
                mention_mod = True
                action_taken = (f"Attempted to delete, but don't have "
                                f"`Manage Messages` in "
                                f"{message.channel.mention}.")
        if rule.additional_actions:
            actions = []
            for action_template in rule.additional_actions:
                action = proto.Action()
                action.CopyFrom(action_template)
                action.guild_id = guild.id
                action.user_id = message.author.id
                if not action.HasField('reason'):
                    action.reason = f"Triggered message filter: '{rule.name}'"
                actions.append(action)
            tasks.append(
                    self.bot.action_manager.sequentially_execute(actions))

        if mention_mod or action_taken:
            text = action_taken + reasons_block
            if mention_mod:
                _, mention_text = utils.mention_random_online_mod(guild)
                text = mention_text + " " + text
            embed = embed_utils.message_to_embed(message)
            tasks.append(guild.get_modlog().send(content=text, embed=embed))

        try:
            await asyncio.gather(*tasks)
        except discord.Forbidden:
            pass

    async def get_rule_reason(self, message, criteria):
        reasons = []
        for regex in criteria.matches:
            if re.search(regex, message.content):
                reasons.append("Message contains banned word or phrase.")

        if criteria.includes_slurs:
            for word in message.content.split():
                if SLUR_FILTER.match(word):
                    reasons.append(
                            f"Message contains recognized racial slur: {word}")
                    break

        if criteria.includes_invite_links:
            if any(invite.get_discord_invite_codes(message.content)):
                reasons.append("Message contains Discord invite link.")

        reasons += list(self.get_mention_reason(message, criteria.mentions))
        reasons += list(self.get_embed_reason(message, criteria.embeds))

        exclude_criteria = (
            # Exclude the owner of the bot and the owner of the server.
            await self.bot.is_owner(message.author),
            message.guild.owner_id == message.author.id,
            # Exclude moderators and bots if configured.
            criteria.exclude_moderators and utils.is_moderator(message.author),
            criteria.exclude_bots and message.author.bot,
            # Exclude specific channels when configured.
            message.channel.id in criteria.excluded_channels,
        )
        if any(exclude_criteria):
            reasons.clear()
        return reasons

    def get_mention_reason(self, message, criteria):
        def check_counts(name, limits, msg_mentions):
            unique_mentions = set(msg_mentions)
            if limits.HasField('maximum_total') and \
               len(msg_mentions) > limits.maximum_total:
                yield (f"Total {name} more than the server limit "
                       f"({limits.maximum_total}).")
            if limits.HasField('maximum_unique') and \
               len(unique_mentions) > limits.maximum_unique:
                yield (f"Unique {name} more than the server limit "
                       f"({limits.maximum_unique}).")

        users = mentions.get_user_mention_ids(message.content)
        roles = mentions.get_role_mention_ids(message.content)

        user_mentions = ["u{id}" for id in users]
        role_mentions = ["r{id}" for id in roles]
        all_mentions = user_mentions + role_mentions

        yield from check_counts("user mentions", criteria.role_mention,
                                user_mentions)
        yield from check_counts("role mentions", criteria.role_mention,
                                role_mentions)
        yield from check_counts("mentions", criteria.any_mention, all_mentions)

    def get_embed_reason(self, message, criteria):
        embed_urls = {e.url for e in message.embeds}
        attachment_urls = {a.url for a in message.attachments}
        unique_embeds = embed_urls | attachment_urls
        if criteria.HasField('max_embed_count') and \
           len(unique_embeds) > criteria.max_embed_count:
            yield (f"Message has {len(unique_embeds)} embeds or attachments. "
                   f"More than the server maximum of "
                   f"{criteria.max_embed_count}.")
