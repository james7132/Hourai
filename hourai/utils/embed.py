import traceback
from discord import Embed
from utils import format, consts

def ellipsize(txt, keep_end=False):
    return format.ellipsize(txt, consts.DISCORD_MAX_EMBED_DESCRIPTION_SIZE, 
                            keep_end=keep_end)

def text_to_embed(txt, keep_end=False):
    embed = Embed(description=ellipsize(txt, keep_end=keep_end))
    return embed

def traceback_to_embed(keep_end=False):
    return text_to_embed(format.multiline_code(traceback.format_exc()), keep_end=keep_end)
