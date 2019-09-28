import discord
from discord.ext import commands
from datetime import datetime
from hourai import bot
from hourai.db import proxies
from hourai.utils import format, success


class ModLogging(bot.BaseCog):
    """ Cog for logging Discord and bot events to a servers' modlog channels.
    """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    def _get_guild_proxy(self, guild_id):
        if guild_id is None:
            return None
        guild = self.bot.get_guild(guild_id)
        if guild is None:
            return None
        return proxies.GuildProxy(guild, self.bot.create_storage_session())

    @commands.Cog.listener()
    async def on_raw_message_delete(self, payload):
        proxy = self._get_guild_proxy(payload.guild_id)
        if proxy is None or not proxy.logging_config.log_deleted_messages:
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
        proxy = self._get_guild_proxy(payload.guild_id)
        if proxy is None or not proxy.logging_config.log_deleted_messages:
            return
        content = '{} messages bulk deleted in <#{}>.'.format(
            len(payload.message_ids), payload.channel_id)
        await proxy.send_modlog_message(content=content)

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    async def log(self, ctx):
        pass

    @log.command(name='deleted')
    async def log_deleted(self, ctx):
        proxy = ctx.get_guild_proxy()
        conf = proxy.logging_config
        conf.log_deleted_messages = not conf.log_deleted_messages
        proxy.save()
        ctx.session.commit()
        change = ('enabled' if conf.log_deleted_messages else 'disabled.')
        await success(f'Logging of deleted messages has been {change}')
