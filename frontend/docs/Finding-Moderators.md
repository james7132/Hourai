Hourai has several features that rely on "random online moderator pings":
pinging one moderator of the server, who is marked by Discord as online (green
bubble or mobile phone in status), at random. This is useful when the bot
requires human intervention where automated action is no longer sufficient. How
does Hourai find moderators, let alone ones that are online? The following
criteria make a user a moderator:

- User owns the server.
- User has a role that marks them as a moderator. Mod roles have names that
  start with either "mod" or "admin", or have the Administrator permission
  enabled on the role.

This is used in the following features:

- `~pingmod` - a user-triggered "random online moderator ping". Useful when the
  attention of one moderator is needed, instead of the entire server staff.
- [[Validation]] - used to ping online mods to manually verify newly joined
  users that have failed validation.
