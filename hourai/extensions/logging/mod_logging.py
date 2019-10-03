import discord
from discord.ext import commands
from datetime import datetime
from hourai.cogs import BaseCog
from hourai.db import proxies, proto
from hourai.utils import format, success, checks


class ModLogging(BaseCog):
    """ Cog for logging Discord and bot events to a servers' modlog channels.
    """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_raw_message_delete(self, payload):
        guild = self.bot.get_guild(payload.guild_id or 0)
        if guild is None:
            return
        proxy = proxies.GuildProxy(self.bot, guild)
        logging_config = await proxy.get_logging_config()
        if logging_config is None or not logging_config.log_deleted_messages:
            return
        content = 'Message deleted in <#{}>.'.format(payload.channel_id)
        embed = discord.Embed(
            title='ID: {}'.format(payload.message_id),
            color=discord.Colour.dark_red(),
            timestamp=datetime.utcnow())
        msg = payload.cached_message
        if msg is None or msg.author.bot:
            await proxy.send_modlog_message(content=content, embed=embed)
            return
        content = 'Message by {} deleted in {}.'.format(
            msg.author.mention, msg.channel.mention)
        author = msg.author
        embed.description = msg.content
        embed.set_author(name=(f'{author.name}#{author.discriminator} '
                               f'{author.id})'),
                         icon_url=msg.author.avatar_url)
        if len(msg.attachments) > 0:
            attachments = (attach.url for attach in msg.attachments)
            field = format.vertical_list(attachments)
            embed.add_field(name='Attachments', value=field)
        await proxy.send_modlog_message(content=content, embed=embed)

    @commands.Cog.listener()
    async def on_raw_bulk_message_delete(self, payload):
        guild = self.bot.get_guild(payload.guild_id or 0)
        if guild is None:
            return
        proxy = proxies.GuildProxy(self.bot, guild)
        logging_config = await proxy.get_logging_config()
        if logging_config is None or not logging_config.log_deleted_messages:
            return
        content = '{} messages bulk deleted in <#{}>.'.format(
            len(payload.message_ids), payload.channel_id)
        await proxy.send_modlog_message(content=content)

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    @checks.is_moderator()
    async def log(self, ctx):
        pass

    @log.command(name='deleted')
    async def log_deleted(self, ctx):
        proxy = proxies.GuildProxy(ctx.bot, ctx.guild)
        conf = await proxy.get_logging_config()
        conf = conf or proto.LoggingConfig()
        conf.log_deleted_messages = not conf.log_deleted_messages
        await proxy.set_logging_config(conf)
        change = ('enabled' if conf.log_deleted_messages else 'disabled.')
        await success(f'Logging of deleted messages has been {change}')
