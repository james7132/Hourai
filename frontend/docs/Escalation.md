# Escalation

!> Currently, this is a closed beta feature, only available to servers
whitelisted by the bot owners. Contact james7132#1567 on Discord to enable the
feature.

**Escalation Ladders** are core feature of Hourai's moderation suite. They
provide an customizable and auditable way of codifying progressive disciplinary
action for server members. In this system, moderators can escalate users with
simple bot commands, and the bot will apply the appropriate disciplinary action.

For example, Server Awesome has set up a escalation ladder as follows:

1.  Warning - User gets a formal warning from the moderators via DM.
2.  Kick - User gets a further warning via DM, then gets kicked from the server.
3.  Temporary Ban - User gets a further warning via DM, then gets temporarily
    banned for 3 days.
4.  Permanent Ban - User gets a final warning via DM, then gets permanently
    banned from the server.

If Bob violates the server rules and moderator Alice decides that he's out of
line, she can escalate him. His first offense will result in a warning (level
1). The second time she escalates him results in a warning followed by a kick.
The third time, it temp bans him, and so on.

## Configuration

!> Configuration is currently only available to the bot owner and cannot be done
by others. Please contact james7132#1567 on Discord to have them configure the
ladder for you.

The escalation ladder for each server is divided into rungs. Each rung can have
a sequence of Actions that are executed in order when a user is escalated.
Almost every Action is supported by escalation ladders, the sole exception being
escalation itself. For more information, see the articles on [[Actions]].

## Escalating Users

`~escalate <reason> <users>` is used to escalate users. This command is only
usable by [[moderators|Finding Moderators]]. A reason is required for auditing
purposes and cannot be empty. If the reason contains spaces, remember to use
quotes (i.e. `~escalate "Test reason" @Bob`). Multiple users can be specified to
batch escalate users if need be.

The opposite can be done by the `~deescalate <reason> <user>` command. It's
operates in the same way the `~escalate` command does, only in the opposite
direction.

## Logging

Any and all escalation actions are logged to the server's modlog. The channel
may be set during automatic setup, or manually via `~setmodlog <channel>`.

## Auditing

Every escalation action is recorded for auditing purposes. The date and time,
the moderator who authorized the escalation, the specific action taken, and the
reason for the escalation.

Use `~escalate history <user>` to view that user's history of escalations.

## Automatic Deescalation

Sometimes it's no longer appropriate to hold users responsible for actions they
did a very long time ago. Each rung in the ladder can be configured with an
optional expiration time.

Taking from the example above, if the expiration on each rung are:

1.  Warning - Never.
2.  Kick - 90 days.
3.  Temporary Ban - 270 days.
4.  Permanent Ban - Never.

If Bob is escalated to the temporary ban level, after 270 days, he'll
automatically return to the Kick level, then after another 90 days, he'll return
to the warning level, but will never drop below it without manual deescalation.
Likewise, escalation overrides these timers, if Bob gets to Kick, then is
escalated again 89 days later, the automatic deescalation does not occur and the
full time to return to the Warning level will encompass the full 90 + 270 days.

Like manual deescalation, this will be logged in both the modlog and in the
user's escalation history.

It's generally advised to set long expiration times (on the order of months to
years) as shorter times can be abused.

## Automation Integration

!> This is a tentative future feature that currently does not exist.

As the Automation module supports running escalation actions, it will be
possible to configure automatic escalations in response to events. For example,
it can be configured to respond to ping spam with automatic escalation.
