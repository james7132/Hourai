import asyncio
import discord
import functools
import inspect
import pytimeparse
import random
import re
import time
from hourai import config
from datetime import timedelta

MODERATOR_PREFIX = 'mod'
DELETED_USER_REGEX = re.compile(r'Deleted User [0-9a-fA-F]{8}')


def clamp(val, min_val, max_val):
    return max(min(val, max_val), min_val)


async def get_user_async(bot: discord.Client, user_id: int) \
        -> discord.User:
    try:
        user = bot.get_user(user_id)
        return user or (await bot.fetch_user(user_id))
    except discord.NotFound:
        return None


async def get_member_async(guild: discord.Guild, user_id: int) \
        -> discord.Member:
    member = guild.get_member(user_id)
    if member:
        return member

    members = await guild.query_members(limit=1, user_ids=[user_id],
                                        cache=True)
    if members is None or len(members) <= 0:
        return None
    return next(iter(member))


async def broadcast(channels, *args, **kwargs):
    """
    Broadcasts a message to multiple channels at once.
    Channels must be an iterable collection of MessageChannels.
    """
    tasks = [ch.send(*args, **kwargs) for ch in channels if ch is not None]
    return await asyncio.gather(*tasks)


async def success(ctx, suffix=None):
    success = config.get_config_value(config.get_config(), 'success_response',
                                      default=':thumbsup:')
    if suffix:
        success += f": {suffix}"
    await ctx.send(success)


async def wait_for_confirmation(ctx):
    def check(m):
        content = m.content.casefold()
        return (content.startswith('y') or content.startswith('n')) and \
            m.author == ctx.author and \
            m.channel == ctx.channel

    try:
        msg = await ctx.bot.wait_for('message', check=check, timeout=600)
        return msg.content.casefold().startswith('y')
    except asyncio.TimeoutError:
        await ctx.send('No resposne found in 10 minutes. Cancelling.',
                       delete_after=60)
        return False


def pretty_print(resource):
    output = []
    if hasattr(resource, 'name'):
        output.append(resource.name)
    if hasattr(resource, 'id'):
        output.append('({})'.format(resource.id))
    return ' '.join(output)


async def maybe_coroutine(f, *args, **kwargs):
    value = f(*args, **kwargs)
    if inspect.isawaitable(value):
        return await value
    return value


async def collect(async_iter):
    vals = []
    async for val in async_iter:
        vals.append(val)
    return vals


def log_time(func):
    """ Logs the time to run a function to std out. """
    if inspect.iscoroutinefunction(func):
        @functools.wraps(func)
        async def async_time_logger(*args, **kwargs):
            real_time = time.time()
            cpu_time = time.process_time()
            try:
                await func(*args, **kwargs)
            finally:
                # TODO(james7132): Log this using logging
                real_time = time.time() - real_time
                cpu_time = time.process_time() - cpu_time
                print('{} called. real: {} s, cpu: {} s.'.format(
                      func.__qualname__, real_time, cpu_time))
        return async_time_logger

    @functools.wraps(func)
    def time_logger(*args, **kwargs):
        real_time = time.time()
        cpu_time = time.process_time()
        try:
            func(*args, **kwargs)
        finally:
            # TODO(james7132): Log this using logging
            real_time = time.time() - real_time
            cpu_time = time.process_time() - cpu_time
            print('{} called. real: {} s, cpu: {} s.'.format(
                  func.__qualname__, real_time, cpu_time))
    return time_logger


async def send_dm(user, *args, **kwargs):
    """ Shorthand to send a user a DM. """
    if user is None:
        return
    dm_channel = user.dm_channel or await user.create_dm()
    await dm_channel.send(*args, **kwargs)


def any_in(population, seq):
    return any(val in population for val in seq)


def is_deleted_user(user):
    """ Checks if a user is deleted or not by Discord. Works on discord.User
    and discord.Member.
    """
    if user is None:
        return None
    return (user.avatar is None and DELETED_USER_REGEX.match(user.name)) or \
        user.discriminator == 0


def is_deleted_username(username):
    """ Checks if a username is deleted or not by Discord.  """
    return DELETED_USER_REGEX.match(username)


def is_moderator(member):
    """ Checks if a user is a moderator. """
    if member is None or not hasattr(member, 'roles') or member.bot:
        return False
    return any(is_moderator_role(r) for r in member.roles)


def is_moderator_role(role):
    """ Checks if a role is a moderator role. """
    if role is None:
        return False
    return (role.permissions.administrator or
            role.name.lower().startswith(MODERATOR_PREFIX))


def is_online(member):
    return member is not None and member.status == discord.Status.online


def all_with_roles(members, roles):
    """Filters a list of members to those with roles. Returns a generator of
    discord.Member objects.
    """
    role_set = set(roles)
    return filter(lambda m: any_in(role_set, m.roles), members)


def all_without_roles(members, roles):
    """Filters a list of members to those without roles. Returns a generator of
    discord.Member objects.
    """
    role_set = set(roles)
    return filter(lambda m: not any_in(role_set, m.roles), members)


def find_moderator_roles(guild):
    """Finds all of the moderator roles on a server. Returns an generator of
    roles.
    """
    return filter(lambda r: is_moderator_role(r), guild.roles)


def find_moderators(guild):
    """Finds all of the moderators on a server. Returns a generator of members.
    """
    return filter(lambda m: m is not None and not m.bot,
                  all_with_roles(guild.members, find_moderator_roles(guild)))


def find_online_moderators(guild):
    """Finds all of the online moderators on a server. Returns a generator of
    members.
    """
    return filter(is_online, find_moderators(guild))


def mention_random_online_mod(guild):
    """Mentions a of a currently online moderator.
    If no moderator is online, returns a ping to the server owner.

    Returns a tuple of (Member, str).
    """
    moderators = list(find_online_moderators(guild))
    if len(moderators) > 0:
        moderator = random.choice(moderators)
        return moderator, moderator.mention
    else:
        return guild.owner, f'{guild.owner.mention}, no mods are online!'


def is_nitro_booster(bot, member):
    """Checks if the user is boosting any server the bot is on."""
    if member is None:
        return False
    return any(m.id == member.id
               for g in bot.guilds
               for m in g.premium_subscribers)


def has_nitro(bot, member):
    """Checks if the user currently has or has previous had Nitro, may have
    false negatives. Cannot have false positives. Checks:
     - Has animated avatar
     - Has the Early Supporter badge
     - Is boosting a server the bot is on
     - Has a custom status with a custom emoji
    """
    if member is None:
        return False
    try:
        has_nitro_activity = member.activity.emoji.is_custom_emoji()
    except AttributeError:
        has_nitro_activity = False
    return any((member.is_avatar_animated(),
                member.public_flags.early_supporter,
                is_nitro_booster(bot, member),
                has_nitro_activity))


def human_timedelta(time_str):
    seconds = pytimeparse.parse(time_str)
    if seconds is None or not isinstance(seconds, int):
        raise ValueError
    return timedelta(seconds=seconds)
