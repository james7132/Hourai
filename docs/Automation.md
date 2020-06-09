# Automation

!> This is currently a tentative future feature, and currently does not exist.

The **Automation** module allows for customizable automatic actions in response
to user activity in servers.

## Automated Actions

The system is built on a list of rules, which is comprised of a trigger,
filters, and a sequence of [[Actions]]. A trigger is fired each time an event
occur, filters can help limit the scope in which the event applies, and the
actions are run in response to it.

### Available Triggers

- On Message - fired when messages are created, edited, or deleted.
- On Join - fired when new users join the server.
- On Leave - fired when users leave the server.
- On Ban - fired when users leave the server.
- On Verify - fired when a user is passes automatic or manual verification

### Available Filters

- Content / Username - filter based on the text content of a message or a user's
  username/nickname.

### Example rule sets

1. Trigger: On Verify, Filter: Username includes "Boss"
1. Action 1 - Direct Message: "You're the boss!"
1. Action 2 - Give Role: DaBoss
1. Trigger: On Message, Filter: Content includes "(╯°□°）╯︵ ┻━┻"
1. Action 1 - Send Message: "(╯°□°）╯︵ ┻━┻"

The two rules above result in verified users with the name "Boss" getting DMed
and given a special role, for the bot to respond with a table flip when other
users send it.

## Auto-moderator

Auto-moderator offers more specialized features that aim to curb specific bad
behavior. Auto-moderator provides specialized rules that trigger on certain
conditions.

- Word Filters - Deletes messages that meet specific content criteria
  - Swear Filter - uses a pre-made list of common swears.
  - Slur Filter - uses a pre-made list of common slurs.
  - Custom Filter - a customizable list of words or phrases to delete.
- Rate Limits - Triggers a set of [[Actions]] in response to users exceeding
  specific limits. Mainly used to curb spam.
  - Ping Limit - limits the number of individual user pings a user can make in a
    time period.
  - Embed Limit - limits the number of embeds/attachments users can post to a
    channel in a time period.
