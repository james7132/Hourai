import asyncio
import discord
import functools
import inspect
import random
import re
import time
from hourai import config

MODERATOR_PREFIX = 'mod'
DELETED_USER_REGEX = re.compile('Deleted User\s+[0-9a-fA-F]+')

async def broadcast(channels, *args, **kwargs):
    """
    Broadcasts a message to multiple channels at once.
    Channels must be an iterable collection of MessageChannels.
    """
    tasks = [ch.send(*args, **kwargs) for ch in channels if ch is not None]
    return await asyncio.gather(*tasks)

async def success(ctx, suffix=None):
    if suffix:
        await ctx.send(f'{config.SUCCESS_RESPONSE}: {suffix}')
    else:
        await ctx.send(config.SUCCESS_RESPONSE)

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
    return async_time_logger if inspect.iscoroutinefunction(func) else time_logger

async def send_dm(user, *args, **kwargs):
    """ Shorthand to send a user a DM. """
    dm_channel = user.dm_channel or await user.create_dm()
    await dm_channel.send(*args, **kwargs)

def any_in(population, seq):
    return any(val in population for val in seq)

def is_deleted_user(user):
    """ Checks if a user is deleted or not by Discord. Works on discord.User
    and discord.Member.
    """
    return user.avatar is None and DELETED_USER_REGEX.match(user.name)

def is_moderator(member):
    """ Checks if a user is a moderator. """
    return any(is_moderator_role(r) for r in member.roles)

def is_moderator_role(role):
    """ Checks if a role is a moderator role. """
    return role.permissions.administrator or role.name.lower().startswith(MODERATOR_PREFIX)

def is_online(member):
    return member.status == discord.Status.online

def all_with_roles(members, roles):
    """ Filters a list of members to those with roles. Returns a generator of members """
    role_set = set(roles)
    return filter(lambda m: any_in(role_set, m.roles), members)

def all_without_roles(members, roles):
    """ Filters a list of members to those without roles. Returns a generator of members """
    role_set = set(roles)
    return filter(lambda m: not any_in(role_set, m.roles), members)

def find_moderator_roles(guild):
    """ Finds all of the moderator roles on a server. Returns an generator of roles """
    return filter(lambda r: is_moderator_role(r), guild.roles)

def find_moderators(guild):
    """ Finds all of the moderators on a server. Returns a generator of members. """
    return all_with_roles(guild.members, find_moderator_roles(guild))

def find_bots(guild):
    """ Finds all of the bots on a server. Returns a generator of members. """
    return filter(lambda m: m.bot, guild.members)

def find_online_moderators(guild):
    """ Finds all of the online moderators on a server. Returns a generator of members. """
    return filter(is_online, find_moderators(guild))

def mention_random_online_mod(guild):
    """
    Returns a string containing a mention of a currently online moderator.
    If no moderator is online, returns a ping to the server owner.
    """
    moderators = list(find_online_moderators(guild))
    if len(moderators) > 0:
        return random.choice(moderators).mention
    else:
        return f'{ctx.guild.owner.mention}, no mods are online!'
