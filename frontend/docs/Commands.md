# Commands

Many of the commands of the bot take parameters to control what they do.
Commands parameters are separated by spaces (' '). If a parameter you are using
contains a space (i.e. a name like "Bot Commander") use double quotes (") to
tell the bot it is a single parameter.

- `user(s)` - Can be a user's mention, username, nickname, user ID, or
  username/discriminator.
- `role(s)` - Can be a role's mention, name, or ID.
- `channel(s)` - Can be a channel's mention, name, or ID.

## Standard Commands

### `~echo <message>`

Repeats a message in chat.

### `~choose <choice(s)>`

Randomly chooses one of the provided selections. Keep in mind that choices are
seperate parameters, and thus follow the same [parameter rules](#Parameter
Legend) as any other command.

### `~invite`

Provides a link to add the bot to any server.

### `~avatar <user(s)>`

Provides a link to the avatar image for all provided users. If no user is
provided, it shows the caller's avatar instead.

### `~whois <User>`

Provides detailed information about a member. Example output:

### `~serverinfo`

Provides detailed information about the current server. Example output:

## Moderation Commands

### `~kick <user(s)>`

**Requires:** Kick Members (Both bot and user) Kicks all provided users from the
current server.

### `~softban <user(s)>`

**Requires:** Ban Members (Bot), Kick Members (User) Bans all provided users from the
current server. Does not need users to be on server, though will need the exact
user ID to run this.

### `~ban <user(s)>`

**Requires:** Ban Members (Both bot and user) Bans all provided users from the
current server. Does not need users to be on server, though will need the exact
user ID to run this.

### `~mute <user(s)>`

**Requires:** Mute Members (Both bot and user) Server mutes all provided users.

### `~unmute <user(s)>`

**Requires:** Mute Members (Both bot and user) Server unmutes all provided
users.

### `~deafen <user(s)>`

**Requires:** Deafen Members (Both bot and user) Server deafen all provided
users.

### `~undeafen <user(s)>`

**Requires:** Deafen Members (Both bot and user) Server undeafens all provided
users.

### `~move <src> <dst>`

**Requires:** Move Members (Both bot and user) Moves all users from `src` voice
channel to `dst` voice channel.

### `~nickname <nickname> <user(s)>`

**Requires:** Manage Nicknames (bot) **Requires:** Manage Nicknames (user), if
you are changing someone else's nickname. **Requires:** Change Nicknames (user),
if you are changing your own nickname. Changes the nicknames of all provided
users to a specified name. Must be shorter than 32 characters.

### `~role add <role> <user(s)>`

**Requires:** Manage roles (user and bot) Adds a role to all provided users.

### `~role remove <role> <user(s)>`

**Requires:** Manage roles (user and bot) Removes a role to all provided users.

### `~temp ban <time> <user(s)>`

**Requires:** Ban Members (user and bot) Temporarily bans all provided users
from the server. Unbans them after the time is up. The user will be notified by
DM, if possible.

### `~temp mute <time> <user(s)>`

**Requires:** Mute Members (user and bot) Temporarily mutes all provided users.
Unmutes them the time is up.

### `~temp unmute <time> <user(s)>`

**Requires:** Mute Members (user and bot) Temporarily unmutes all provided
users. Mutes them the time is up.

### `~temp deafen <time> <user(s)>`

**Requires:** Deafen Members (user and bot) Temporarily deafens all provided
users. Undeafens themr the time is up.

### `~temp undeafen <time> <user(s)>`

**Requires:** Deafen Members (user and bot) Temporarily undeafens all provided
users. Deafens them after the time is up.

### `~temp role add <time> <user(s)>`

**Requires:** Manage roles (user and bot) Temporarily adds a role to all
provided users. Removes it after the time is up.

### `~temp role remove <time> <user(s)>`

**Requires:** Manage roles (user and bot) Temporarily removes a role from all
provided users. Adds it back after the time is up.

### `~escalate <reason> <user(s)>`

## Logging Commands

!> These command will be removed once the web control panel is public, as they
these options will be configurable from the control panel.

### `~setmodlog <channel>`

Sets the server's modlog to the specified channel.

### `~log deleted`

Enables logging of deleted messages to the modlog.

## Feed Commands

!> These command will be removed once the web control panel is public, as they
these options will be configurable from the control panel.

These commands all control automatics feeds. They pull information from Discord
or other services, and automatically provide posts to the established channels.
These include:

- Discord server announcements (join, leave, ban, voice)
- Reddit posts to subscribed subreddits.

All of the commands in this module require the following permissions:
**Requires:** Send Messages (bot **Requires:** Manage Server (user)

### `~announce join`

Starts/stops the bot from announcing users joining the server in the current
channel.

### `~announce leave`

Starts/stops the bot from announcing users leaving/being kicked from the server
in the current channel.

### `~announce ban`

Starts/stops the bot from announcing bans from the server in the current
channel.

### `~announce voice`

Starts/stops the bot from announcing joining/leaving/moving users within voice
channels in the current channel.

### `~reddit add <Subreddit(s)>`

Starts subreddit feed(s) from all provided subreddits into the current channel.

### `~reddit remove <Subreddit(s)>`

Stops subreddit feed(s) from all provided subreddits into the current channel.

## Music Commands

If a text channel is configured for the music features (default: #music-bot),
this

### `~play <query|url>`

Adds a piece of music to the music queue. Must be used while in a voice channel.
If music isn't already playing, the bot will join your voice channel and start
playing.

Supports links from the following services:

 - YouTube
 - SoundCloud
 - Bandcamp
 - Shoutcast
 - Direct File links (\*.mp3, \*.wav, etc)

Can also be used by DJs to continue playback if the bot is paused.

### `~nowplaying`

Displays a live updating UI with information about the currently playing song.

### `~queue`

Displays a live updating UI with information about the songs currently in the
queue.

### `~skip`

Casts a vote to skip the current song. Over 50% of the current users in the voice
channel must vote to skip before the song is skipped.

If called by the user who requested the song, it will always skip regardless of
how many votes there are.

## DJ Commands

### `~pause`

Pauses playback from the bot. Use `~play` to resume playback.

### `~volume <vol>`

Changes the volume of the playback. Accepted range is 0 to 150. If called without
a volume value (i.e. `~volume`), it will respond with the current volume setting.

### `~forceskip`

Forcibly skipss the currently playing song, regardless of how many votes to skip
have been cast.

### `~stop`

Stop playing music, clears the queue, and has the bot leave the voice channel.
