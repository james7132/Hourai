# Hourai

Hourai is a Discord Bot focused on safety, security, administration, and
automation. Written in Python 3.7+. Hourai is a bot built to help ease moderation
of communities of any size, offering features that work with servers of 10 users
to those with well over 100,000.

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
 * Identity Tracking Tools - users changing usernames to avoid punishment? Hourai
   keeps track of their 20 latest usernames.

Informative / Fun Features

 * Feeds - pulls near-realtime feeds from a variety of sources, including Reddit,
   RSS, Danbooru, etc.
 * Music Bot - play music from YouTube, SoundCloud, and other public services.
   (Not yet implemented)

## Development/Self-Hosting

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

### Building the Bot
While written in Python, the bot needs to be built into a container to run in the standard enviroment. Building the bot can be done with the following command.

```bash
docker-compose build
```

### Launching the Bot
Launching the bot can be done with the following command.
```bash
docker-compose up
```

### Iterating on Changes
Rebuilding, relaunching, and viewing logs for the bot while in development can be
done with the following command:
```bash
docker-compose build && docker-compose up && docker logs hourai -f
```

### Running as a persistent daemon
Launching the bot as a background daemon can be done with the following command.
```bash
docker-compose up -d
```

To ensure the bot is restarted on system restart, be sure to have the docker
system service enabled. For systemd machines, this can be done with the following
command:

```bash
systemctl enable docker.service
```
