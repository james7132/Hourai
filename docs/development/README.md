# Development

?> This is a document about setting up a development enviroment for creating new
features. For information on running your own instance of the bot, see
[Self-Hosting](development/self-hosting.md).

If you don't want to use the provided bot, you can host your own version of the
bot.

If the above does not deter you, or you are interested in developing features on
top of the exisitng bot, please refer to the following section.

## Setup

The standard way of developing requires use of Docker and Docker Compose. See
the [official guides](https://docs.docker.com/compose/install/) on how to
install them.

At the time of writing, the bot is tuned to run on a VPS with 1 CPU core and 2GB
of RAM. If your development/production enviroment is different, you may need to
readjust the container limits.

## Configuration

Before any development or self-hosting can be done, users will need to configure
the bot with several secrets to ensure external services are contacted
correctsly. Hourai uses [JSONNET](https://jsonnet.org/) to configure the bot
itself. Other dependent services have config files that also need to be
configured. All of these config files have example configs under
`config-example/`. Be sure to fill out the relevant information in each of them,
then rename the directory to `config/`.

## Building the Bot

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

## Extending or Adding Features

Hourai's code, as pertains to interactions with Discord, are done via discord.py
extensions: Python modules that are dynamically loaded at runtime. By default,
Hourai automatically loads all top level modules under `hourai/extensions/` as
extensions. To add additional functionality, implement a new extension/cog under
that folder. Please refer to the
[official discord.py documentation](https://discordpy.readthedocs.io/en/latest/ext/commands/cogs.html)
for more information.
