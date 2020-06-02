This is a public changelog of the alterations made to the bot, including both
code and operational changes.

Questions regarding the bot's use? Join the public development server for Hourai
here: https://discord.gg/UydKWHX.

### WIP (TBD)

 * [Validation] Removed redundant reasons in validation reports.
 * [Validation] Users that have an exact avatar match with a banned user will now
   be rejected.

### v1.2.0 (05/26/2020)

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
 * [Escalation] New beta feature: Escalation. A customizable way of codifying
   progressive moderation action to reduce moderation mental overhead. Full
   documentaion can be found on [the
   wiki](https://github.com/james7132/Hourai/wiki/Escalation-Ladder). Currently
   only available in select
   servers (must be explicitly enabled by the bot owner). Will enter general
   availability when the web interface launches.
 * [Technical] Migrated Hourai to PostgreSQL instead of using SQLite.

### v1.1.1 (05/21/2020)

 * [Music] Hotfix: Fixing ~forceskip from erroring out.

### v1.1.0 (05/20/2020)

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
