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
    components = []
    hours, seconds = divmod(seconds, 3600)
    if hours != 0:
        components.append(str(hours))
    minutes, seconds = divmod(seconds, 60)
    mins = str(minutes)
    if len(components) > 0:
        mins.zfill(2)
    components.append(mins)
    components.append(str(seconds).zfill(2))
    return ':'.join(components)


def progress_bar(percent, size=12):
    return ''.join('\uD83D\uDD18' if idx == int(percent * size) else '▬'
                   for idx in range(size))
