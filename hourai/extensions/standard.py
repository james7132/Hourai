import discord
import itertools
import random
import re
import typing
from hourai import utils
from hourai.cogs import BaseCog
from hourai.utils import embed, format
from discord.ext import commands


class Standard(BaseCog):

    def __init__(self):
        super().__init__()

    @commands.command()
    async def echo(self, ctx, *, content: str):
        await ctx.send(content)

    @commands.command()
    async def choose(self, ctx, *choices: str):
        """ Randomly chooses between several different choices. """
        try:
            choice = random.choice(choices)
            await ctx.send(f'I choose {format.simple_code(choice)}!')
        except IndexError:
            await ctx.send("There's nothing to choose from!")

    @commands.command()
    @commands.guild_only()
    async def playing(self, ctx, *, game: str):
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)
        regex = re.compile(game)

        def activities():
            for member in ctx.guild.members:
                for activity in member.activities:
                    activity_str = (activity.name
                                    if isinstance(activity, discord.Activity)
                                    else str(activity))
                    if regex.search(activity_str):
                        yield (activity_str, member)
        def member_list(members):
            return format.comma_list(m[1].display_name for m in members)
        member_activities = sorted(activities(), key=lambda x: x[0])
        lines = [f'**{k}**: {member_list(v)}'
                 for k, v in itertools.groupby(member_activities,
                 lambda x: x[0])]
        if len(lines) <= 0:
            await ctx.send('Nobody found!')
        else:
            await ctx.send(format.vertical_list(lines))

    @commands.command()
    @commands.guild_only()
    async def serverinfo(self, ctx):
        guild = ctx.guild
        owner = guild.owner
        embed = discord.Embed(title=f'{guild.name} ({guild.id})',
                              description=guild.description)
        embed.set_thumbnail(url=guild.icon_url)
        embed.add_field(name='Owner',
                        value=f'{owner.name}#{owner.discriminator}')
        embed.add_field(name='Members', value=str(guild.member_count))
        embed.add_field(name='Created On', value=str(guild.member_count))
        embed.add_field(name='Region', value=str(guild.region))
        if guild.premium_subscription_count:
            value = (f'{guild.premium_subscription_count} '
                     f'(Tier {guild.premium_tier})')
            embed.add_field(name='Boosters', value=value)
        if guild.features:
            embed.add_field(name='Features',
                            value=format.code_list(guild.features))
        await ctx.send(embed=embed)

    # @commands.command()
    # async def convert(self, ctx, src_unit, dst_unit):
        # """ Converts units. (i.e. 2.54cm in -> 1 inch) """
        # try:
        # quantity = self.units.Quantity(src_unit).to(dst_unit).to_compact()
        # await ctx.send(format.code(str(quantity)))
        # except:
        # src, dst = format.code(src_unit), format.code(dst_unit)
        # await ctx.send(f'Failed to convert from {src} to {dst}')

    @commands.command()
    async def avatar(self, ctx, *users: discord.User):
        if len(users) <= 0:
            users = [ctx.author]
        await ctx.send(format.vertical_list(str(u.avatar_url) for u in users))

    @commands.command()
    async def invite(self, ctx):
        app_info = await ctx.bot.application_info()
        link = discord.utils.oauth_url(
                        str(app_info.id),
                        permissions=discord.Permissions.all())
        await ctx.send(f'Use this link to add me to your server: {link}')

    @commands.command()
    @commands.guild_only()
    async def pingmod(self, ctx):
        """
        Pings a moderator on the server. Mod roles begin with "mod" or
        "admin" or have the administrator permission.
        """
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)
        await ctx.send(utils.mention_random_online_mod(ctx.guild))

    @commands.command()
    async def whois(self, ctx, user: typing.Union[discord.Member,
                                                  discord.User]):
        await ctx.send(embed=embed.make_whois_embed(ctx, user))


def setup(bot):
    standard = Standard()
    bot.add_cog(standard)
    bot.help_command.cog = standard
