# Privacy Information

!> This in no way constitutes a legally binding privacy policy, but rather is a
living document of the data collected and utilizied by the bot.

Hourai is a bot built for security in mind. To do this job, Hourai collects some
information about users it sees on Discord. Below is a comprehensive list of
data types and how long Hourai retains them after their deletion from Discord.

- Usernames - Hourai stores the last 20 usernames of every user it has
  visibility of. This includes the associated user ID, username, discriminator,
  and timestamp when it was first seen. This is queryable via the `whois`
  command and upon verification of users.
- Bans - Hourai caches information about bans in all servers it has access to.
  Including the server ID, user ID, user avatar hash, and the ban reason. To
  avoid providing this data to Hourai, remove the `Ban Members` permission from
  the bot. This is used to provide more accurate information for validation.
  This cache is wiped and repopulated approximately every day.
- Member Roles - Hourai stores a copy of all role ids of server members, even
  after they leave the server. This is used to allow Hourai to restore roles to
  users that leave and rejoin the server. All role information about a server is
  deleted upon removing the bot from a server.
- Server Invites - On servers where the bot has verification enabled and has
  Manage Server, Hourai caches a local copy of all server invites for the
  explicit puprose of finding which server invite was used by a new member to
  join the server during verification. This information is not stored in any
  persistent way and is immediately removed upon either a bot restart or the
  bot's removal from the server.
- Reddit Posts - the titles of posts on these sites may appear in Hourai's logs,
  even if the post was deleted from the source site. No public way of seeing the
  output of these logs is available. These logs are persisted for a maximum of
  30 days.
- Message content and metadata are cached for up to 24 hours for the purposes of
  logging deleted and edited messages to a server's modlog channel. Upon
  deleting and/or editing messages, a log message to a server's modlog will be
  created mirroring the deleted/edited content. Cached messages that are deleted
  on Discord's end will then immediately be removed from the cache.
- Message content and metadata from the Discord gateway are also used, but not
  stored, for the purposes of automated message filtering.
- All other data used by Hourai is pulled transitively from the Discord Gateway
  and is removed upon invalidation of that state (i.e. a user's nickname change
  will be wiped as soon as the gateway reports it.)

If you would like for any of the above data to be cleared from Hourai's
database, please contact `james7132#1567` (User ID: 151215593553395721) on
Discord to have the data deleted. Be aware that this data collection cannot be
disabled, even upon request of deleting stored data.
