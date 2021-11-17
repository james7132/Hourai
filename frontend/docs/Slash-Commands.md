# Slash Commands

[Slash Commands](slash-commands) are a new first-party way of using commands on
Discord. The available slash commands are listed here, many of which have
[normal command](Commands.md) parallels.

None of the parameters are listed on this page as Discord will provide a guide
of which parameter is required and which are optional. Unlike normal commands,
parameter order does not matter and can be used in any order.

?> Permissions, unless specified otherwise, require that both the bot and the
calling user must have the specified Discord permissions.

!> Slash Commands must be explicitly enabled on a server when the bot is joins.
If typing `/` in chat does not bring up a prompt to use these commands with the
bot, please use this [link](authorize) to authorize use of these commands.

## Standard Commands

|Command|Permissions|Description|
|:------|:----------|:----------|
|`/pingmod`|None|Anonymously pings one of the online moderators |
|`/pingmod`|None|Provides detailed help on how to use a command.|

## Moderation Commands

?> A good number of these commands take optional `reason` parameter. This will
be written into the audit log when provided.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`/kick`|Kick Members|Kicks all provided users from the serverr.|
|`/ban`|Ban Members|Bans all provided users from the server.  Can be used with user IDs to ban users outside of the server.|
|`/mute`|Mute Members|Server mutes all provided users.|
|`/deafen`|Deafen Members|Server deafen all provided users.|
|`/move`|Move Members|Moves all users from one voice channel to another.|
|`/role add`|Manage Roles|Adds a role to all provided users.|
|`/role remove`|Manage Roles|Removes a role to all provided users.|
|`/prune`|Manage Messages|Deletes the last `count` messages. Defaults to 100.\*|
|`/escalate up`|Escalate Members|Escalates all provided user in accordance with the server's confiugred escalation ladder.|
|`/escalate down`|Escalate Members|Deescalates all provided user in accordance with the server's confiugred escalation ladder.|
|`/escalate history`|None|Shows the escalation history for a given user.|

\* - Prune commands cannot delete messages older than 14 days.

## Music Commands

!> If a text channel is configured for the music features (default: #music-bot),
these commands can only be used in that channel.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`/music play`|None|Adds a piece of music to the music queue.|
|`/music nowplaying`|None|Displays a live updating UI with information about the currently playing song.|
|`/music queue`|None|Displays a live updating UI with information about the songs currently in the queue.|
|`/music skip`|None|Casts a vote to skip the current song. Over 50% of the current users in the voice channel must vote to skip before the song is skipped.|
|`/music pause`|DJs|Pauses playback from the bot.|
|`/music unpause`|DJs|Unpauses playback from the bot.|
|`/music volume`|DJs|Changes the volume of the playback. Range is 0-150.|
|`/music stop`|DJs|Stop playing music, clears the queue, and has the bot leave the voice channel.|

[slash-commands]: https://support.discord.com/hc/en-us/articles/1500000368501-Slash-Commands-FAQ
[authorize]: https://discord.com/api/oauth2/authorize?client_id=208460637368614913&scope=bot%20applications.commands
