import discord
import typing
from hourai import bot
from hourai.utils import embed
from discord.ext import commands


class Standard(bot.BaseCog):

    def __init__(self):
        super().__init__()

    # @commands.command()
    # async def echo(self, ctx, *, content: str):
        # await ctx.send(content)

    # @commands.command()
    # async def choose(self, ctx, *choices: str):
        # """ Randomly chooses between several different choices. """
        # try:
        # choice = random.choice(choices)
        # await ctx.send(f'I choose {format.simple_code(choice)}!')
        # except IndexError:
        # await ctx.send("There's nothing to choose from!")

    # @commands.command()
    # async def convert(self, ctx, src_unit, dst_unit):
        # """ Converts units. (i.e. 2.54cm in -> 1 inch) """
        # try:
        # quantity = self.units.Quantity(src_unit).to(dst_unit).to_compact()
        # await ctx.send(format.code(str(quantity)))
        # except:
        # src, dst = format.code(src_unit), format.code(dst_unit)
        # await ctx.send(f'Failed to convert from {src} to {dst}')

    # @commands.command()
    # async def avatar(self, ctx, *users: discord.User):
        # if len(users) <= 0:
        # users = [ctx.author]
        # await ctx.send(format.vertical_list(u.avatar_url for u in users))

    # @commands.command()
    # async def invite(self, ctx):
        # link = discord.utils.oauth_url(CLIENT_ID,
        # permissions=discord.Permissions.all())
        # await ctx.send(f'Use this link to add me to your server: {link}')

    # @commands.command()
    # @commands.guild_only()
    # async def pingmod(self, ctx):
        # """
        # Pings a moderator on the server. Mod roles begin with "mod" or
        # "admin" or have the administrator permission.
        # """
        # if not ctx.guild.chunked:
        # await ctx.bot.request_offline_members(ctx.guild)
        # await ctx.send(utils.mention_random_online_moderator(ctx.guild))

    @commands.command()
    async def test_whois(self, ctx,
                         user: typing.Union[discord.Member, discord.User]):
        await ctx.send(embed=embed.make_whois_embed(ctx, user))

    @commands.command()
    async def test_help(self, ctx):
        await ctx.send_help()


def setup(bot):
    bot.add_cog(Standard())
