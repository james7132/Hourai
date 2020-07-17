import discord
import itertools
import random
import re
import typing
import collections
import logging
from datetime import datetime, timedelta
from discord.ext import commands
from hourai import utils
from hourai.bot import cogs
from hourai.db import proto, models
from hourai.utils import embed, format, checks
from sqlalchemy.orm.exc import NoResultFound


DICE_REGEX = re.compile(r"(\d+)d(\d+)(.?)(\d*)")
FDICE_REGEX = re.compile(r"(\d+)df(.?)(\d*)")
Dice = collections.namedtuple("Dice", "count min max modifier mod_type")


def die(val):
    dice_match = DICE_REGEX.match(val)
    fdice_match = FDICE_REGEX.match(val)
    match = dice_match or fdice_match
    if not match:
        raise Exception("Input does not match a dice description.")
    min_val = -2 if match == fdice_match else 1
    max_val = 2 if match == fdice_match else int(match.group(2))
    try:
        modifier = int(match.group(4))
    except Exception:
        modifier = 0
    return Dice(count=int(match.group(1)), min=min_val, max=max_val,
                modifier=modifier, mod_type=match.group(3))


class Standard(cogs.BaseCog):

    def __init__(self):
        super().__init__()

    @commands.command()
    async def echo(self, ctx, *, content: str):
        await ctx.send(content)

    @commands.command()
    async def roll(self, ctx, *dice: die):
        """ Rolls some dice

        Example usages:
            ~roll 3d6
            ~roll 3d6 2d8
            ~roll 3d6+5
            ~roll 3d6-5
            ~roll 3d6*5
            ~roll 3d6/5
            ~roll 3d6^5
        """
        total_count = sum(d.count for d in dice)
        if total_count > 150:
            await ctx.send('Cannot roll more than 150 dice at once.')
            return
        if any(d.modifier < 0 or d.modifier > 99 for d in dice):
            await ctx.send('Any modifier must be in the range of 0-99.')
            return

        rolls = []
        total = 0
        for die in dice:
            dice_rolls = list(random.randint(die.min, die.max)
                              for x in range(die.count))
            rolls.extend(dice_rolls)
            sub_total = sum(dice_rolls)
            logging.info(die.mod_type)
            sub_total = {
                "+": lambda x: x + die.modifier,
                "-": lambda x: x - die.modifier,
                "/": lambda x: x / die.modifier,
                "*": lambda x: x * die.modifier,
                "x": lambda x: x * die.modifier,
                "^": lambda x: x ** die.modifier,
            }.get(die.mod_type, lambda x: x)(sub_total)
            total += sub_total
        rolls.sort()
        resp = [f"Rolled a total of `{total}` from {len(rolls)} rolls:"]
        resp.append(format.multiline_code(
                    format.comma_list(str(r) for r in rolls)))
        await ctx.send('\n'.join(resp))

    @commands.command()
    async def choose(self, ctx, *choices: str):
        """ Randomly chooses between several different choices. """
        try:
            choice = random.choice(choices)
            await ctx.send(f'I choose {format.simple_code(choice)}!')
        except IndexError:
            await ctx.send("There's nothing to choose from!")

    @commands.command()
    async def remindme(self, ctx, time: utils.human_timedelta, reminder: str):
        """Schedules a reminder for the future. The bot will send a direct
        message to remind you at the approriate time.

        To avoid abuse, can only schedule events up to 1 year into the future.

        Examples:
            ~remindme 30m Mow the lawn!
            ~remindme 6h Poke Bob about dinner.
            ~remindme 90d Send mom Mother's Day gift.
        """
        if time > timedelta(days=365):
            await ctx.send(
                "Cannot schedule reminders more than 1 year in advance!",
                delete_after=90)

        action = proto.Action()
        action.user_id = ctx.author.id
        action.direct_message.content = f"Reminder: {reminder}"

        scheduled_time = datetime.utcnow() + time
        ctx.bot.action_manager.schedule(scheduled_time, action)
        await ctx.send(
            f"You will be reminded via direct message at {scheduled_time}.",
            delete_after=90)

    @commands.command()
    @commands.guild_only()
    async def playing(self, ctx, *, game: str):
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
        """Displays detailed information about the server."""
        guild = ctx.guild
        owner = guild.owner
        msg_embed = discord.Embed(title=f'{guild.name} ({guild.id})',
                                  description=guild.description)
        msg_embed.set_thumbnail(url=guild.icon_url)
        msg_embed.add_field(name='Owner',
                            value=f'{owner.name}#{owner.discriminator}')
        msg_embed.add_field(name='Members', value=str(guild.member_count))
        embed._add_time_field(msg_embed, 'Created On', guild.created_at,
                              datetime.utcnow())
        msg_embed.add_field(name='Region', value=str(guild.region))
        if guild.premium_subscription_count:
            value = (f'{guild.premium_subscription_count} '
                     f'(Tier {guild.premium_tier})')
            msg_embed.add_field(name='Boosters', value=value)
        if guild.features:
            msg_embed.add_field(name='Features',
                                value=format.code_list(guild.features))
        await ctx.send(embed=msg_embed)

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    async def tag(self, ctx, *, tag: str):
        """Allows tagging text for later retreival.

        If used without a subcomand, it will search the tag database for the tag
        requested.
        """
        db_tag = ctx.session.query(models.Tag).get(
                (ctx.guild.id, tag.casefold()))

        response = db_tag.response if db_tag else f"Tag `{tag}` does not exist."
        await ctx.send(response)

    @tag.command(name="set")
    @commands.guild_only()
    @checks.is_moderator()
    async def tag_set(self, ctx, tag: str, *, response: str = None):
        """Sets a tag.

        If the tag doesn't exist, a tag for the name will be created.
        If the tag exists, it'll be updated:
           ~tag set hi Hello World!

        If the response is empty, the tag will be deleted.
           ~tag set hi
        """
        lower_tag = tag.casefold()
        query = ctx.session.query(models.Tag).filter_by(
                guild_id=ctx.guild.id, tag=lower_tag)

        if not response:
            query.delete()
            ctx.session.commit()
            await ctx.send(f"Tag `{tag}` deleted")
            return

        try:
            db_tag = query.one()
            db_tag.response = response
        except NoResultFound:
            db_tag = models.Tag(guild_id=ctx.guild.id, tag=lower_tag,
                                response=response)

        ctx.session.add(db_tag)
        ctx.session.commit()
        await ctx.send(f"Tag `{tag}` set!")

    @tag.command(name="list")
    @commands.guild_only()
    async def tag_list(self, ctx):
        """Lists all available tags."""
        db_tags = ctx.session.query(models.Tag.tag) \
                             .filter_by(guild_id=ctx.guild.id) \
                             .order_by(models.Tag.tag) \
                             .all()
        await ctx.send(
            format.code_list(tag for tag, in db_tags) or
            "No tags have been set! Use `~tag set` to make some.")

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
        online_mod, mention = utils.mention_random_online_mod(ctx.guild)
        await ctx.send(mention)

    @commands.command()
    async def whois(self, ctx, user: typing.Union[discord.Member,
                                                  discord.User]):
        await ctx.send(embed=embed.make_whois_embed(ctx, user))


def setup(bot):
    standard = Standard()
    bot.add_cog(standard)
    bot.help_command.cog = standard
