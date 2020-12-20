import discord
from discord.ext import commands
from hourai.bot import cogs
from hourai.utils import embed as embed_utils
from hourai.utils import format, success, checks


class ModLogging(cogs.BaseCog):
    """ Cog for logging Discord and bot events to a servers' modlog channels.
    """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    def should_log(self, channel_id, config):
        if not config.enabled:
            return False
        if channel_id in config.channel_filter.denylist:
            return False
        if len(config.channel_filter.allowlist) > 0:
            return channel_id in config.channel_filter.allowlist
        return True

    @commands.Cog.listener()
    async def on_message_edit(self, before, after):
        if before.guild is None:
            return
        proxy = self.bot.get_guild_proxy(before.guild)
        config = (await proxy.config.get('logging')).edited_messages
        channel = guild.get_channel(config.output_channel_id)
        if not should_log(payload.channel_id, config) or \
           channel is None or msg.author.bot:
            return
        content = (f'Message by {msg.author.mention} edited in '
                   f'{msg.channel.mention}.')
        embed = embed_utils.message_to_embed(before)
        embed.description = None
        embed.color = discord.Color.dark_orange()
        embed.add_field(name="Before", value=before.content)
        embed.add_field(name="After", value=after.content)
        await modlog.send(content=content, embed=embed)

    @commands.Cog.listener()
    async def on_raw_message_delete(self, payload):
        guild = self.bot.get_guild(payload.guild_id or 0)
        if guild is None:
            return
        proxy = self.bot.get_guild_proxy(guild)
        config = (await proxy.config.get('logging')).delete_messages
        channel = guild.get_channel(config.output_channel_id)
        if not should_log(payload.channel_id, config) or channel is None:
            return
        content = f'Message deleted in <#{payload.channel_id}>.'
        msg = payload.cached_message
        embed = embed_utils.message_to_embed(msg or payload.message_id)
        embed.color = discord.Color.dark_red()
        if msg is not None:
            if msg.author.bot:
                return
            content = (f'Message by {msg.author.mention} deleted in '
                       f'{msg.channel.mention}.')
            if len(msg.attachments) > 0:
                attachments = (attach.url for attach in msg.attachments)
                field = format.vertical_list(attachments)
                embed.add_field(name='Attachments', value=field)
        await modlog.send(content=content, embed=embed)

    @commands.Cog.listener()
    async def on_raw_bulk_message_delete(self, payload):
        guild = self.bot.get_guild(payload.guild_id or 0)
        if guild is None:
            return
        proxy = self.bot.get_guild_proxy(guild)
        config = (await proxy.config.get('logging')).delete_messages
        channel = guild.get_channel(config.output_channel_id)
        if not should_log(payload.channel_id, config) or channel is None:
            return
        content = (f'{len(payload.message_ids)} messages bulk deleted in '
                   f'<#{payload.channel_id}>.')
        await channel.send(content=content)

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    @checks.is_moderator()
    async def log(self, ctx):
        pass

    @log.command(name='deleted')
    async def log_deleted(self, ctx):
        """ Enables/disables logging of deleted messages in the current
        channel.
        """
        config = await ctx.guild_proxy.config.get('logging')
        config.deleted_messages.enabled = not config.deleted_messages.enabled
        config.deleted_messages.output_channel_id = ctx.channel.id
        change = ('enabled' if config.deleted_messages.enabled else 'disabled.')
        await ctx.guild_proxy.config.set('logging', config)
        await success(ctx,
            f'Logging of deleted messages has been {change} '
            f'in {ctx.channel.mention}.')

    @log.command(name='edited')
    async def log_edited(self, ctx):
        """ Enables/disables logging of edited messages in the current
        channel.
        """
        config = await ctx.guild_proxy.config.get('logging')
        config.edited_messages.enabled = not config.edited_messages.enabled
        config.edited_messages.output_channel_id = ctx.channel.id
        change = ('enabled' if config.edited_messages.enabled else 'disabled.')
        await ctx.guild_proxy.config.set('logging', config)
        await success(ctx,
            f'Logging of edited messages has been {change} '
            f'in {ctx.channel.mention}.')
