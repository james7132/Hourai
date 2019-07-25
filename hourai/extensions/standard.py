import discord
import pint
import random
import typing
from hourai import bot, utils
from hourai.utils import format
from discord.ext import commands

CLIENT_ID = '208460637368614913'


class Standard(bot.BaseCog):

    def __init__(self):
        super().__init__()
        units = pint.UnitRegistry()

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
    async def convert(self, ctx, src_unit, dst_unit):
        """ Converts units. (i.e. 2.54cm in -> 1 inch) """
        try:
            quantity = self.units.Quantity(src_unit).to(dst_unit).to_compact()
            await ctx.send(format.code(str(quantity)))
        except:
            src, dst = format.code(src_unit), format.code(dst_unit)
            await ctx.send(f'Failed to convert from {src} to {dst}')

    @commands.command()
    async def avatar(self, ctx, *users: discord.User):
        if len(users) <= 0:
            users = [ctx.author]
        await ctx.send(format.vertical_list(u.avatar_url for u in users))

    @commands.command()
    async def invite(self, ctx):
        link = discord.utils.oauth_url(CLIENT_ID,
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
        await ctx.send(utils.mention_random_online_moderator(ctx.guild))

    @commands.command()
    async def whois(self, ctx, user: typing.Union[discord.Member, discord.User]):
        lines = []
        def add_field(field_name, value):
            if value:
                lines.append(f'{field_name}: {format.code(str(value))}')
        username_line = f'{user.name}#{user.discriminator} ({user.id})'
        if user.bot:
            username_line += ' [BOT]'
        add_field('Username', username_line)
        add_field('Nickname', getattr(user, 'nick', None))
        add_field('Created on', getattr(user, 'created_at', None))
        add_field('Joined on', getattr(user, 'joined_on', None))
        add_field('Boosting Since', getattr(user, 'premium_since', None))
        self.__add_roles(user.roles, add_field)
        guild_count = len(ctx.bot.get_all_matching_members(user))
        if guilds_seen_on > 1:
            lines.append(
                'Seen on {format.bold(str(guild_count))} other servers.')
        lines.append(
            user.avatar_url if user.avatar else user.default_avatar_url)
        # TODO(james7132): Add old usernames
        await ctx.send(format.vertical_list(lines))

    def __add_roles(self, roles, add_field):
        if len(roles) <= 1:
            return
        roles = iter(roles)
        roles.next()
        add_field('Roles', format.code_list(r.name for r in reversed(roles)))


def setup(bot):
    pass
    # bot.add_cog(Standard())
