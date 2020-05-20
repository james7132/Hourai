PLAY_EMOJI = '\u25B6'
PAUSE_EMOJI = '\u23F8'
STOP_EMOJI = '\u23F9'

PREV_PAGE_EMOJI = '\u25C0'
NEXT_PAGE_EMOJI = '\u25B6'

DIVIDENDS = (3600, 60)
MAX_DURATION = 0x7FFFFFFFFFFFFFFF


def time_format(seconds):
    if seconds == MAX_DURATION:
        return "LIVE"
    seconds = round(seconds / 1000.0)
    hours, seconds = divmod(seconds, 3600)
    minutes, seconds = divmod(seconds, 60)
    components = []
    if hours != 0:
        components.append(hours)
    components.append(minutes)
    components.append(seconds)
    return ':'.join(str(comp).zfill(2) for comp in components)


def progress_bar(percent, size=12):
    return ''.join('\uD83D\uDD18' if idx == int(percent * size) else '▬'
                   for idx in range(size))
