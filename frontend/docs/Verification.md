# Verification

The main focus of the verification feature is on isolating potentially troublesome
users before they have a chance to cause chaos on servers, and provide a "herd
immunity" style of protection. Upon joining a server where Hourai's verification
feature is enabled, Hourai will run through a silo of criteria to determine if a
user is considered a potential issue. If the user passes all of these checks,
they are given a verification role, which can be any aribtrary Discord role.
Typically, the lack of this role will prevent the user from being able to see
most channels in the server, but this is dependent on how the role's Discord
permissions are set up.

Hourai's verification process is tuned to be high precision, low recall. If
configured to run automatically, the vast majority of users will not be
conscious of the process. At the time of writing, of the 7,200 members on the
largest server with the feature enabled, only 100 or so have needed to deal with
verification (~1.38% of the population).

Part of the verification process includes cross-checks against the banlists of
other servers that Hourai is on. This provides a form of herd-immunity by
failing to verify users that have been banned from other servers. To prevent
potential abuse, Hourai only sources ban information from servers with more than
150 members. This number is subject to change, ignores bot users, and servers
may be excluded based on the discretion of the bot maintainer(s). This also
pulls the ban reason for why the user was banned to provide additional context.
Thus to help other servers, if you are banning users from your server, be sure
to provide a verbose and descriptive reason for why they are being banned. This
banlist is internally refreshed every few minutes, so it may not be up to date
in realtime. To avoid providing this information to Hourai, disable the Ban
Members permission for the bot.

## Enabling Verification

?> If you only want Hourai to provide verification context, and not perform
automatic verification, you only need to do steps 3 and 4. This is useful if you
don't want to lockdown your server, or if you want to manually verify all
incoming users regardless of automatic verification status.

### Step 1: Creating a Verification Role

Create a new role, name it whatever you feel like. Common names include
"Verified", checkmarks, etc. Make sure it mirrors all of the permissions the
@everyone role has, because at the end of this, the new verification role will be
replacing the @everyone role.

### Step 2: Creating a Landing Channel

This is a channel visible only to unverified users. Create a text channel and
deny read and send permissions to the @everyone role, along with overrides for
the verification role that do allow for it. This makes it the only channel visible
to unverified users. You may want to explicitly deny embed permissions to avoid
unverified users from spamming the channel. Ensure that mods and admins have
access to this channel, as it will be used as an isolation area for interacting
with unverified users.

It's recommended to leave a message in that channel that prompts new joins to
answer some preliminary questions. This avoids letting scraping user boots,
which usually do not interact with people in text chat, through the verification
process.

You may need someone to test this permission scheme before proceeding.

### Step 3: Enabling Verification

Enable verification with the following command and the created verification role.

```
~verification setup <role>
```

If you don't want to lockdown your server, omit the role from the command.

### Step 4: Setting up a Verification Log (Optional)

This is an optional but highly recommended step. To get a better understanding
of why Hourai rejected a user, it is useful to set up a modlog channel where
Hourai can output the verification details. Either create a text channel or use a
preexisting one with the following command to set the modlog for your server:

```
~setmodlog <channel>
```

This will output detailed information regarding the verification results of every
new user that joins the server.

Normal users will appear as follows:

![](https://i.imgur.com/Aj5tLtW.png)

Users that fail verification will have a log message like this:

![](https://i.imgur.com/c7yKQ85.png)

These messages will also [[ping one random online moderator|Finding Moderators]]
to try to verify the user that has joined the server. If no moderator is online,
it will instead ping the server owner.

### Step 3: Propagating the Verified Role

The next step is to ensure that everyone who should have the role has it. Run
the following command to give everyone the role.

```
~verification propagate
```

This will run Hourai's verification checks on every member currently on the
server and give those that pass the verification role. If you have a large
server, this can take a while. The response will be updated with it's progress.
Once it says it is done, please check via Server Settings at least most of the
members have the role.

Members that fail verification may need to be checked by the server moderation
team manually.

### Step 4: Removing the permissions from the @everyone Role

The final step is to cut off unverified members access to the server. Remove all
of the basic permissions that have been moved to the Verified role. Typically
this means the @everyone role has no permissions.

## Testing Verifcation

Assuming things went smoothly, you may need to test that the system works
properly. Either have an existing verified member leave and rejoin, or make a
test account to ensure that the verification is working as intended.

## Manual Verification

Hourai's verifcation system isn't perfect by any means, and errors on the side
of caution. Instead of taking significant moderation action upon failure to
verify, Hourai defers any actual action to a real human. If a modlog has been
set, upon failure to verify, Hourai will ping one random online moderator to
manually verify the new join. Moderators are automatically found via role. The
role must either have the Administrator permission, or the role name must start
with "Admin" or "Mod" (case insensitive). Moderators MUST be set to an online
status to be pinged. If the circle is not green, they will not be pinged for
manual verification. If no moderators are found, the server owner will be pinged
instead, regardless of whether they are online or not.

To verify the user, simply provide the user the verification role manually. This
can be done in any way that gives the user the role (i.e. bot, manual role
grants, etc).

It is suggested to give new joins the benefit of the doubt when doing manual
verification. Be sure to check these common points before making the judgement
call of whether to approve them or not.

- User's avatar.
- User's username and username history.
- Potential reasons for rejection.

## Lockdowns

Sometimes, it's necessary to lock down the server to prevent all new people
joining from interacting in the server proper. To temporarily lockdown a server,
use the `~verification lockdown <time>` command. This will force all verifications
to be manual until the time passes. This lockdown is either lifted as the time
expires, or manually via `~verification lockdown lift`. This can be useful when
the server is being raided.

## Disabling Verification
If the feature is no longer necessary, use `~verification disable` to disable
verification. To reenable it, rerun `~verification setup <role>` as if you were
setting up verification from scratch.

## Appendix: Verification Criteria

It was originally planned to have these criteria configurable, but for now it's
hard-coded and cannot be changed on a per-server basis. In the future this may
change.

Hourai's verification system uses tiered approvers and rejectors. Higher tier
approvers and rejectors will override any lower level decision made, and are
intended to become increasingly specific in the group they target. By default,
Hourai will approve incoming users. If a rejector fires on the user, they will
be rejected and will require manual verification. If a higher level approver
supersedes the rejector, the user is approved. This process is repeated until In
the following table, the approvers and rejectors are listed in increasing level
order, so the lower on the table an approver/rejector is, the higher precedence
it will take.

| Name                      | Type      | Level        | Description                                                                                                                                   |
| :------------------------ | :-------- | :----------- | :-------------------------------------------------------------------------------------------------------------------------------------------- |
| New Account               | Rejector  | Suspicion    | New accounts are commonly used by alts and user bots. Rejects any account less than 30 days old.                                              |
| No Avatar                 | Rejector  | Suspicion    | Accounts without avatars are common for alts and user bots. Rejects any account without a avatar.                                             |
| Deleted Account           | Rejector  | Suspicion    | Deleted accounts should not be joining new servers.                                                                                           |
| Link based Username       | Rejector  | Suspicion    | Rejects accounts that have links in their username. Common for advertising user bots.                                                         |
| Nitro                     | Approver  | Suspicion    | Accounts with Nitro tend not to be user bots or alts. Bots cannot read profile information, so this may not fire for every account with Nitro |
| Moderator/Bot Name Match  | Rejector  | Questionable | Close matches with moderator or bots may be an attempt at impersonation.                                                                      |
| Offensive/Sexual Username | Rejector  | Questionable | Common for trolls to use these kinds of usernames. Usually asks for manual verification before joining.                                       |
| Banned User               | Rejector  | Malice       | Checks all servers that Hourai is in to see if the user is banned. Rejects users that are banned from servers with over 150 users in them.    |
| Banned Username           | Rejector  | Malice       | Rejects users that have a case-insensitive exact match with pre-existing banned users on the current server.                                  |
| Distinguished Users       | Approvers | Malice       | Approves users that are owners of Partnered or Verified servers.                                                                              |
| Raid                      | Rejector  | Malice       | Rejects all users while a "raid mode" is enabled for the server.                                                                              |
| Bot                       | Approver  | Override     | Bots added by moderators will not be rejected.                                                                                                |
| Owner                     | Approver  | Override     | Owner of the bot cannot be rejected.                                                                                                          |

Here are a list of potential planned approvers/rejectors:

| Name                 | Type     | Level     | Description                                                                                                           |
| :------------------- | :------- | :-------- | :-------------------------------------------------------------------------------------------------------------------- |
| Inappropriate Avatar | Rejector | Suspicion | Uses Google Cloud Vision API to check if a user's avatar is NSFW or not, rejects NSFW or potentially violent avatars. |
