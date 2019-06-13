import discord
import pint
import random
import typing
from hourai import bot
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
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)

        owner = ctx.guild.owner

        def is_mod_role(role):
            role_name = role.name.lower()
            return (role.permissions.administrator or
                    role_name.startswith('mod') or
                    role_name.startswith('admin'))

        roles = set(role.id for role in ctx.guild.roles if is_mod_role(role))

        def is_online_mod(self, member):
            return (member.status == discord.Status.online and
                    (len(roles.intersection(m.roles)) > 0 or owner == member))

        matching_members = [m for m in ctx.guild.members if is_online_mod(m)]
        if len(matching_members) > 0:
            response = random.choice(matching_members).mention
        else:
            response = f'{owner.mention}, no mods are online!'

        await ctx.say(response)

    @command.commands()
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
    bot.add_cog(Standard())
