# Commands

Many of the commands of the bot take parameters to control what they do.
Commands parameters are separated by spaces (' '). If a parameter you are using
contains a space (i.e. a name like "Bot Commander") use double quotes (") to
tell the bot it is a single parameter.

To see

- `user(s)` - Can be a user's mention, username, nickname, user ID, or
  username/discriminator.
- `role(s)` - Can be a role's mention, name, or ID.
- `channel(s)` - Can be a channel's mention, name, or ID.

?> Permissions, unless specified otherwise, that both the bot and the calling
user must have the specified Discord permissions.

## Standard Commands

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~help <message>`|None|Provides detailed help on how to use a command.|
|`~echo <message>`|None|Repeats a message in chat.|
|`~choose <choice(s)>`|None|Randomly chooses one of the provided selections.|
|`~invite`|None|Provides a link to add the bot to any server.|
|`~avatar <user(s)>`|None|Provides a link to the avatar image for all provided users. If no user is provided, it shows the caller's avatar instead.|
|`~whois <User>`|None|Provides detailed information about a user.|
|`~serverinfo`|None|Provides detailed information about the current server.|
|`~tag <tag>`|None|Fetches a tag, a snippet of saved text.|
|`~tag set <tag> <response>`|Moderator|Sets a tag.|
|`~tag list`|None|Lists all of the tags in the server.|
|`~remindme <duration> <reminder>`|None|Schedules Hourai to send the user as a DM
in the future. Time uses shorthand (i.e. 2d for 2 days into the future).|

## Moderation Commands

?> A good number of these commands take optional `reason` parameter. This will
be written into the audit log when provided.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~kick <user(s)> <reason>`|Kick Members|Kicks all provided users from the serverr.|
|`~ban <user(s)> <reason>`|Ban Members|Bans all provided users from the server.  Can be used with user IDs to ban users outside of the server.|
|`~softban <user(s)> <reason>`|Kick Members (User), Ban Members (Bot)|Bans then unbans a user from the server, deleteing the last 7 days of messages from them
without leaving a lasting ban.|
|`~mute <user(s)> <reason>`|Mute Members|Server mutes all provided users.|
|`~unmute <user(s)> <reason>`|Mute Members|Server unmutes all provided users.|
|`~deafen <user(s)>`|Deafen Members|Server deafen all provided users.|
|`~undeafen <user(s)>`|Deafen Members|Server undeafens all provided users.|
|`~move <src> <dst>`|Move Members|Moves all users from one voice channel to another.|
|`~nickname <nickname> <user(s)>`|Manage Nicknames|Changes the nicknames of all provided users to a specified name. Must be shorter than 32 characters.|
|`~role add <role> <user(s)>`|Manage Roles|Adds a role to all provided users.|
|`~role remove <role> <user(s)>`|Manage Roles|Removes a role to all provided users.|
|`~temp ban <time> <user(s)>`|Ban Members|Temporarily bans a user from the server. The bot will automatically undo it when time is up.|
|`~temp mute <time> <user(s)>`|Mute Members|Temporarily mutes all provided users. The bot will automatically undo it when time is up.|
|`~temp unmute <time> <user(s)>`|Mute Members| Temporarily unmutes all provided users. The bot will automatically undo it when time is up.|
|`~temp deafen <time> <user(s)>`|Deafen Members|Temporarily deafens all provided users. The bot will automatically undo it when time is up.|
|`~temp undeafen <time> <user(s)>`|Deafen Members|Temporarily undeafens all users. The bot will automatically undo it when time is up.|
|`~temp role add <time> <role> <user(s)>`|Deafen Members|Temporarily adds a role to all provided users. The bot will automatically undo it when time is up.|
|`~temp role remove <time> <role> <user(s)>`|Deafen Members|Temporarily removes a role to all provided users. The bot will automatically undo it when time is up.|
|`~escalate <reason> <user(s)>`|Escalate Members|Escalates all provided user in accordance with the server's confiugred escalation ladder.|
|`~escalate history <user>`|None|Shows the escalation history for a given user.|

## Logging Commands

!> These command will be removed once the web control panel is public, as they
these options will be configurable from the control panel.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~setmodlog <channel>`|Manage Guild (User)|Sets the server's modlog to the specified channel.|
|`~log deleted`|Manage Guild (User)|Toggles whehter deleted messages are logged to the server's modlog.|

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

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~announce join`|Manage Guild (User)|Starts/stops the bot from announcing users joining the server in the current channel.|
|`~announce leave`|Manage Guild (User)| Starts/stops the bot from announcing users leaving from the server in the current channel.|
|`~announce ban`|Manage Guild (User)|Starts/stops the bot from announcing bans from the server in the current channel.|
|`~announce voice`|Manage Guild (User)|Starts/stops the bot from announcing joining/leaving/moving users within voice channels in the current channel.|

## Music Commands

!> If a text channel is configured for the music features (default: #music-bot),
these commands can only be used in that channel.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~play <query|url>`|None|Adds a piece of music to the music queue.|
|`~nowplaying`|None|Displays a live updating UI with information about the currently playing song.|
|`~queue`|None|Displays a live updating UI with information about the songs currently in the queue.|
|`~skip`|None|Casts a vote to skip the current song. Over 50% of the current users in the voice channel must vote to skip before the song is skipped.|

## DJ Commands

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~pause`|DJs|Pauses playback from the bot. Use `~play` to resume playback.|
|`~volume <vol>`|DJs|Changes the volume of the playback. Range is 0-150.|
|`~forceskip`|DJs|Forcibly skipss the currently playing song, regardless of how many votes to skip have been cast.|
|`~stop`|DJs|Stop playing music, clears the queue, and has the bot leave the voice channel.|
