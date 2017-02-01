# Hourai - A Discord Bot

<p align="center">
    <a href="https://opensource.org/licenses/mit-license.php">
        <img src="https://img.shields.io/badge/license-MIT%20License-blue.svg" alt="MIT License">
    </a>
    <a href="https://discordapp.com/oauth2/authorize?client_id=208460637368614913&scope=bot&permissions=0xFFFFFFFFFFFF">
        <img src="https://img.shields.io/badge/discord-add--to--server-738bd7.svg" alt="Add DrumBot to your server!">
    </a>
</p>

* [Features]()
* [Setup]()
* [Collected Information]()
* [License]()
* [Commands]()
 * [Standard Module]()
 * [Admin Module]()
 * [Feeds Module]()
 * [Owner Module]()
* [Configuration]()

###Features
* Administrative - Permission-based text commands that apply administrative actions on users simultaneously
* Custom Commands

### Setup
Simply click this [link](https://discordapp.com/oauth2/authorize?client_id=208460637368614913&scope=bot&permissions=0xFFFFFFFFFFFF) to add Houhai to your server (you need the Manage Server permission to do so)

### License: [MIT](./LICENSE)
### Collected Information
Hourai logs various things and keeps track of many stats to help with functionality. By adding Hourai to your server, you agree to have the following things logged and tracked:

* All moderation changes, and users joining/leaving/banning will also be logged. This is to provide modlog functionality.

### Commands
The following are a non-comprehensive list of all commands that Hourai provides. Some of them require specific permissions on Hourai and/or the user using the command. For the required permissions:
* Unmarked - Both DrumBot and the user invoking the command need the permission.
* Marked with an asterisk (*) - Only the user invoking the command needs the permission.
* Marked with a carrot (^) - Only Hourai needs the permission.

Note, Hourai will ignore all commands enacting administrative action on a role higher than Hourai's highest role.

This is just a general survey of the commands. Please use the `help` command to see the commands you can currently use.

####Standard Module
|Command|Permissions|Notes|
|:--|:--|:--|
|`echo`|N/A|Makes the bot say something.|
|`avatar`|N/A|Gets the avatar of user(s).|
|`serverinfo`|N/A|Provides general information about the current server.|
|`channelinfo`|N/A|Provides general information about the current channel.|
|`whois`|N/A|Provides general information about a specific user.|
|`topic`|N/A|Prints the topic of the current channel.|
|`module enable`|Manage Server|Enables a bot module for the current server|
|`module disable`|Manage Server|Disables a bot module for the current server|
|`command`|At least the 'Command' role|Creates, edits, or removes a custom command|
|`command dump`|N/A|Dumps the source text for a custom command|
|`command role`|Manage Server|Sets the minimum role to create custom commands|
####Admin Module
|Command|Permissions|Notes|
|:--|:--|:--|
|`kick`|Kick Members|Kicks member(s) from the server, can still rejoin|
|`ban`|Ban Members|Bans member(s) from the server|
|`mute`|Mute Members|Server mutes member(s)|
|`unmute`|Mute Members|Server unmutes member(s)|
|`deafen`|Deafen Members|Server deafens member(s)|
|`undeafen`|Deafen Members|Server undeafens members(s)|
|`nickname`|Manage Nicknames|Nicknames member(s)|
|`prune`|Manage Messages|Deletes the last X messages in the current channel|
|`prune user`|Manage Messages|Deletes messages from certain users in the current channel|
|`prune bot`|Manage Messages|Deletes messages from bots in the current channel|
|`prune embed`|Manage Messages|Deletes messages with attachments or embeds in the current channel|
|`prune ping`|Manage Messages|Deletes messages that ping members in the current channel|
|`modlog`|N/A|Uploads an log of moderator changes made to the server|
|`server permissions`|N/A|Lists all of the server permissions for the current user or a specified user|
|`role add`|Manage Roles|Adds a role to 1+ members|
|`role remove`|Manage Roles|Removes a role from member(s)|
|`role ban`|Manage Roles|Prevents 1+ members from getting the specified role through any method.|
|`role unban`|Manage Roles|Allows 1+ members to get the specified role after being banned from it|
|`role nuke`|Manage Roles, Manage Server*|Removes a role from all members on the server|
|`config prefix`|Manage Server*|Sets the bot's command prefix for this server.|
|`channel create`|Manage Channels|Creates a new public text channel|
|`channel delete`|Manage Channels|Deletes channel(s) from the server|
|`channel list`|Manage Channels|Deletes channel(s) from the server|
|`channel permissions`|N/A|Lists the channel-specific permissions for the current user or a specified user for the current channel|
|`temp role add`|Manage Roles|Temporarily adds a role to a user(s)|
|`temp ban`|Ban Members|Temporarily bans a user from the server. Will notify the user via PM.|
####Feeds Module
|Command|Permissions|Notes|
|:--|:--|:--|
|`announce leave`|Manage Server|Enables or disables server leave messages in the current channel|
|`announce join`|Manage Server|Enables or disables server join messages in the current channel|
|`announce ban`|Manage Server|Enables or disables server ban messages in the current channel|
|`announce voice`|Manage Server|Enables or disables voice change messages in the current channel|
####Owner Module
|Command|Permissions|Notes|
|:--|:--|:--|
|`log`|Bot Owner|Gets the bot's log. Returned as file uploaded to Discord.|
|`counters`|Bot Owner|Gets the counter set. Returned as file uploaded to Discord.|
|`broadcast`|Bot Owner|Sends a message to all servers that the bot is on. Use sparingly.|
|`rename`|Bot Owner|Renames the bot.|
|`reavatar`|Bot Owner|Changes the avatar of the bot.|
|`kill`|Bot Owner|Shuts down the bot.|
|`leave`|Bot Owner|Makes the bot leave the current server.|
|`blacklist user`|Bot Owner|Blacklists a user from the bot. They are then entirely ignored by the bot.|
|`blacklist server`|Bot Owner|Blacklists a server from the bot. Bot will subsequently leave the server.|

###Configuration
Hourai currently supports a per-server customization of what command prefix to use. By default this is `~`. However, it can be changed via the `config prefix` command. To see what the current command prefix is, use the `serverinfo` command.
