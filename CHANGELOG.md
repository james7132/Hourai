# Changelog

This is a public changelog of the alterations made to the bot, including both
code and operational changes.

Questions regarding the bot's use? Join the public development server for Hourai
here: https://discord.gg/UydKWHX.

### BETA FEATURES

Beta features are highlighted in bold and are not available to the general public
just yet. These need to be manually enabled and configured by the bot owner(s).

## v1.4.2 (TBD)

 * [Automation] **Beta Feature: Customizable Message Filtering.** Supports
   automatically removing and/or notifying moderators for potentially
   problematic messages. Supports customizable criteria and responses, including
   arbitrary custom actions and integration with the escalation ladder. Example
   potential rules:
   * Message contains slurs -> Delete, notify moderator. (Removal of
     slurs)
   * Message contains banned keywords -> Delete, notify moderator. (Keyword
     filter)
   * Message contains Discord invite -> Delete. (No solicitation rule)
   * Message mentions more than 25 users -> Delete, notify moderator,
     escalate user. (Anti-Ping Spam)
 * [Music Bot] Fixed the music bot stopping whenever Discord resets the voice
   websocket connection. The bot will properly resume playing music when this
   happens.
 * [General] Fixed issue with `~remindme` allowing reminders more than 1 year in
   the future. Added missing `~remindme` documentation to the website.
 * [General] Fixed several crashes that failed if the owner of a server had their
   account deleted.

## v1.4.1 (07/16/2020)

 * [General] Added a `~remindme` command to allow users to set up reminders for
   themselves up to 1 year into the future.
 * [General] Added a `~tag` command for saving and retrieving snippets of text.
 * [Moderation] `~ban` and other moderation commands now support adding a reason
   to the ban. Regardless of whether a reason is provided , the bot will log who
   used the command as a part of the reason.
 * [Validation] Validation messages will now include information about which
   instant invite a user used to join. This will also work with servers with
   vanity URLs. This is only available when the bot has the `Manage Server`
   permissions due to Discord API restrictions.

## v1.4.0 (07/06/2020)

 * [Actions] Fixed crash when sending empty direct messages.
 * [General] Fixed `~move`'s permissions checks.
 * [General] Hourai will no longer mention @everyone, ping roles, or ping users
   unless a feature requires an explicit ping to one specific user (i.e.
   `~pingmod`)
 * [Feeds] `~announce join/leave/ban` have been fixed and will toggle the correct
   announcements in the target channels.
 * [Validation] Removed any potential for false positives in detecting Discord
   Staff or Discord Partners.
 * [Validation] The bot will now approve all Verified Bot Developers as
   distinguished users (like Discord Staff or Partners). This is done as verified
   bot developers strict identity verification and require developing a bot that
   is on more than 75 servers. This is sufficient to pass most forms of
   validation.
 * [Validation] Expanded approval check for Nitro include users with the Early
   Supporter badges.
 * [Validation] Expanded approval check for Nitro to include users with custom
   statuses with custom emoji, which can only be set by users with Nitro.
 * [Validation] Attempts to bypass username filters by using non-ASCII characters
   to avoid direct matches will now fail. (i.e. using the username "ＦＵＣＣＫ"
   will still the match the "fuck" username filter). This applies to the
   following checks:
   - Sexually Inapproriate Usernames
   - Offensive Usernames
   - Banned User Names
 * [Validation] The bot will now reject any user with wide-width unicode
   character usernames as these tend to be disruptive to other users. Examples
   of these kinds of characters can be seen
   [here](https://www.reddit.com/r/Unicode/comments/5qa7e7/widestlongest_unicode_characters_list/).
 * [Validation] The three reaction buttons will now operate as expected:
   - White Check Mark: Verify User. User reacting needs Manage Roles.
   - Red X: Kick User. User reacting needs Kick Users.
   - Skull and Cross Bones: Ban User. User reacting needs Ban Members.
 * [Validation] Actions taken by pressing the reaction buttons on validation logs
   will be logged in the modlog.

## v1.3.0 (06/17/2020)

 * [General] Command will now give more complete explainations when they fail to
   run.
 * [Validation] Removed redundant reasons in validation reports.
 * [Validation] Users that have an exact avatar match with a banned user will now
   be rejected.
 * [Validation] Modlog output for rejected users will now use a mention of
   the user to make it easier to pull up the user's profile.
 * [Validation] Cross server ban checks will now state how many servers a user
   has been banned from.
 * [Validation] Added "~validation purge" command to mass remove unverified
   users. A potential substitute for pruning servers.
 * [Music] When users leave the voice channel, their queued music will now be
   cleared 5 minutes after leaving the channel if they do not return.
 * [Music] Fixed remove and removeall commands erroring out.
 * [Technical] Stopped automatically fetching offline members at startup to
   minimize memory usage. Some commands may take longer to run on larger
   servers, particularly actions that apply to all members of a server.

## v1.2.0 (05/26/2020)

 * [General] Bot will now automatically configure associated channels based on
   pre-existing channels upon joining a server. (i.e. "#modlog" or "#bot-modlog"
   will be set to the bot's modlog channel for the server automatically)
 * [Documentation] Documentation for the more complex features available via the
   bot are now documented via the [GitHub
   wiki](https://github.com/james7132/Hourai/wiki). This will be moved to a
   seperate documentation site in the future.
 * [Validation] Automatic validation will now reject users with username
   histories that may suggest fake account deletion (making it look like they
   deleted their accounts without actually triggering account deletion).
 * [Validation] `~valdiation lockdown` - Temporarily force manual validation for
   all new joins. Useful during raids.
 * [Modedration] **Beta feature: Escalation.** A customizable way of codifying
   progressive moderation action to reduce moderation mental overhead. Full
   documentaion can be found on [the
   wiki](https://github.com/james7132/Hourai/wiki/Escalation-Ladder). Currently
   only available in select
   servers (must be explicitly enabled by the bot owner). Will enter general
   availability when the web interface launches.
 * [Technical] Migrated Hourai to PostgreSQL instead of using SQLite.

## v1.1.1 (05/21/2020)

 * [Music] Hotfix: Fixing ~forceskip from erroring out.

## v1.1.0 (05/20/2020)

 * **Hourai is now a [Verified Discord Bot](https://support.discord.com/hc/en-us/articles/360040720412-Bot-Verification-and-Data-Whitelisting)**
 * [General] Added utilities for making public announcements via modlog channels.
   Will be used to communicate important information regarding the bot to server
   owners and moderators in the future.
 * [General] Improved `~help` documentation on multiple commands.
 * [Validation] Added `~validation verify` for running out-of-band validation.
 * [Validation] Added an Override level approver for approving owners of
   Partnered or Verified servers.
 * [Music] Fixed bug where non-DJ users could use `~stop`.
 * [Music] Improved stability of music bot features. Music bot should be able to
   sustain long queues lasting well over 7 hours.
