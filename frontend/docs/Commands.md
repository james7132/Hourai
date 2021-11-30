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

!> All of these commands have been migrated to [Slash
Commands](Slash-Commands.md). Please use those intead.

## Logging Commands

!> These command will be removed once the web control panel is public, as they
these options will be configurable from the control panel.

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~setmodlog <channel>`|Manage Guild (User)|Sets the server's modlog to the specified channel.|
|`~log deleted`|Manage Guild (User)|Toggles whether deleted messages are logged to the server's modlog.|
|`~log edited`|Manage Guild (User)|Toggles whether edited messages are logged to the server's modlog.|

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
|`~reddit add <subreddit>`|Manage Guild (User)|Adds a subreddit feed to the current channel|
|`~reddit remove <subreddit>`|Manage Guild (User)|Removes a subreddit feed from the current channel|
|`~reddit list`|None|Lists all of the subreddits that have feed in the current channel.|

## Verification Commands

These commands control validation, a system for running background checks on new
joins into a server. For more detailed information, please see
[Verification](Verification.md).

|Command|Permissions|Description|
|:------|:----------|:----------|
|`~validation setup <Role>`|Moderator|Sets up validation.|
|`~validation propagate`|Moderator| Starts/stops the bot from announcing users leaving from the server in the current channel.|
|`~validation verify <user>`|Moderator|Runs validation checks on a member who has already joined the server.|
|`~validation lockdown <timerange>`|Moderator|Temporarily forces all new joins to be manually verified. Good for countering raids.|
|`~validation disable`|Moderator|Disables verification on the server.|
