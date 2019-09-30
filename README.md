# Hourai

Hourai is a Discord Bot focused on safety, security, administration, and
automation. Written in Python 3.7+. Hourai is a bot built to help ease moderation
of communities of any size, offering features that work with servers of 10 users
to those with well over 100,000.

---

## Features

Moderation, Safety, and Security Features

 * Comprehensive Validation System - run background checks against new users.
   Discourages problematic users, limits user bot damage, and curbs abuse.
 * Highly Configurable Automation System - run arbitrary bot commands in response
   to virtually any Discord event. (Partially implemented)
 * Configurable Moderation Utilties - simplify mod mental overhead by
   establishing a well known escalation ladder to deal with problematic users.
   (Partially implemented).
 * Configurable Auto-Mod - automatically respond to known events to apply
   moderation. Integrates tightly with configurable escalation ladders.
 * Community Moderation Tools - take a load off of your mod team by letting the
   community moderate itself. (Not yet  implemented).
 * Anti-Raid Tools - automatically or manually shut down raids (Not yet
   implemented)
 * Identity Tracking Tools - users changing usernames to avoid punishment?
   Hourai keeps track of their 20 latest usernames.

Informative / Fun Features

 * Feeds - pulls near-realtime feeds from a variety of sources, including Reddit,
   RSS, Danbooru, etc.
 * Music Bot - play music from YouTube, SoundCloud, Bandcamp, and other public
   services.  (Mostly implemented)

---

## Development/Self-Hosting
If you don't want to use the provided bot, you can host your own version of the
bot.  Self-hosting is generally ill-advised due to the number of components that
the main bot depends on. Additionally, many of the security benefits of Hourai
arise from the number of servers it operates on. Running Hourai on a limited
number of servers may limit the quality of service provided.

If the above does not deter you, or you are interested in developing features on
top of the exisitng bot, please refer to the following section.

### Setup
The standard way of developing requires use of Docker and Docker Compose. See the
[official guides](https://docs.docker.com/compose/install/) on how to install
them.

An example `docker-compose.yml` file is provided in the root level of this
project. Before launching the bot, be sure to rename it to `docker-compose.yml`
and fill out the relevant configuration options.

At the time of writing, the bot is tuned to run on a VPS with 1 CPU coreand 2GB
of RAM. If your development/deployment enviroment is different, you may need to
readjust the container limits.

### Configuration
Before any development or self-hosting can be done, users will need to configure
the bot with several secrets to ensure external services are contacted
correctsly. Hourai uses [JSONNET](https://jsonnet.org/) to configure the bot
itself. Other dependent services have config files that also need to be
configured. All of these config files have example configs under `config/`. Be
sure to fill out the relevant information in each of them, then rename the files
without the `.example.` in the name (i.e. `hourai.example.jsonnet ->
hourai.jsonnet`). This also needs to be done with the
`docker-compose.example.yml` in the root of the repository.

### Building the Bot
While written in Python, the bot needs to be built into a container to run in
the standard enviroment. Building the bot can be done with the following
command.

```bash
docker-compose build
```

### Launching the Bot
Launching the bot can be done with the following command.
```bash
docker-compose up
```

### Iterating on Changes
Rebuilding, relaunching, and viewing logs for the bot while in development can
be done with the following command:
```bash
docker-compose build && docker-compose up && docker logs hourai -f
```

### Running as a persistent daemon
Launching the bot as a background daemon can be done with the following command.
```bash
docker-compose up -d
```

To ensure the bot is restarted on system restart, be sure to have the docker
system service enabled. For systemd machines, this can be done with the
following command:

```bash
systemctl enable docker.service
```

### Extending or Adding Features
Hourai's code, as pertains to interactions with Discord, are done via discord.py
extensions: Python modules that are dynamically loaded at runtime. By default,
Hourai automatically loads all top level modules under `hourai/extensions/` as
extensions. To add additional functionality, implement a new extension/cog under
that folder. Please refer to the [official discord.py
documentation](https://discordpy.readthedocs.io/en/latest/ext/commands/cogs.html)
for more information.

## Privacy Information
Hourai is a bot built for security in mind. To do this job, Hourai collects some
information about users it sees on Discord. Below is a comprehensive list of
data types and how long Hourai retains them after their deletion from Discord.

 * Usernames - Hourai stores the last 20 usernames of every user it has
   visibility of. This includes the associated user ID, username, discriminator,
   and timestamp when it was first seen. This is queryable via the `whois`
   command and upon verification of users.
 * Bans - Hourai caches information about bans in all servers it has access to.
   Including the server ID, user ID of the banned user, and the ban reason.
   To avoid providing this data to Hourai, remove the `Ban Members` permission
   from the bot.  This is used to provide more accurate information to
   This cache is wiped and repopulated every 5 minutes.
 * Reddit Posts - the titles of posts on these sites may appear in Hourai's logs,
   even if the post was deleted from the source site. No public way of seeing the
   output of these logs is available. These logs are persisted for a maximum of
   30 days.
 * All other data used by Hourai is pulled transitively from the Discord Gateway
   and is removed upon invalidation of that state (i.e. a user's nickname change
   will be wiped as soon as the gateway reports it.)

If you would like for any of the above data to be cleared from Hourai's database,
please contact `james7132#1567` (User ID: 151215593553395721) on Discord to have
the data deleted. Be aware that this data collection cannot be disabled, even
upon request of deleting stored data.
