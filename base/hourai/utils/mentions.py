import re


USER_MENTION_REGEX = re.compile(r"<@\!?(\d+)>")
ROLE_MENTION_REGEX = re.compile(r"<@&(\d+)>")
CHANNEL_MENTION_REGEX = re.compile(r"<@\#(\d+)>")


def get_user_mention_ids(text):
    return USER_MENTION_REGEX.findall(text)


def get_role_mention_ids(text):
    return ROLE_MENTION_REGEX.findall(text)


def get_channel_mention_ids(text):
    return CHANNEL_MENTION_REGEX.findall(text)
