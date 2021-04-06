from .owner_logging import OwnerLogging
from discord.ext import commands
from hourai.bot import cogs
from hourai.utils import checks


class ModLogging(cogs.BaseCog):
    """ Cog for logging Discord and bot events to a servers' modlog channels.
    """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

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
        config = ctx.guild.config.logging
        config.deleted_messages.enabled = not config.deleted_messages.enabled
        config.deleted_messages.output_channel_id = ctx.channel.id
        change = ('enabled' if config.deleted_messages.enabled
                  else 'disabled.')
        await ctx.guild.flush_config()
        await ctx.send(f'Logging of deleted messages has been {change} '
                       f'in {ctx.channel.mention}.')

    @log.command(name='edited')
    async def log_edited(self, ctx):
        """ Enables/disables logging of edited messages in the current
        channel.
        """
        config = ctx.guild.config.logging
        config.edited_messages.enabled = not config.edited_messages.enabled
        config.edited_messages.output_channel_id = ctx.channel.id
        change = ('enabled' if config.edited_messages.enabled else 'disabled.')
        await ctx.guild.flush_config()
        await ctx.send(f'Logging of edited messages has been {change} '
                       f'in {ctx.channel.mention}.')


def setup(bot):
    cogs = (ModLogging(bot), OwnerLogging(bot))
    for cog in cogs:
        bot.add_cog(cog)
