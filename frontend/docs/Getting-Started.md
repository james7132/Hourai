# Getting Started

To start using the bot, use this
[link](https://discordapp.com/oauth2/authorize?client_id=208460637368614913&scope=bot&permissions=0xFFFFFFFFFFFF)
to authorize the bot as a user on your server. **NOTE: the permissions you give
the bot determines what commands are available. You cannot use `~mute` without
giving the bot the Mute Members permission.**

## Automatic Setup

Hourai is a highly customizable bot, and thus has many configuration options to
change and tune. To try to make configuration smoother, upon joining a server,
several configuration options are automatically set up based on some criteria.
All of these options can be edited later, so if it makes an undesirable
configuration, it can be corrected.

- Modlog channel will be set to any text channel with a name containing `modlog`
  (case insensitive) that Hourai can read/send messages to.
- Music bot text channel (the only channel where music commands are allowed)
  will be set to any text channel named `music-bot` (case insensitive, exact
  naming) where Hourai can read from.
- Music bot voice channel (the only channel where music can be played) will be
  set to any voice channel that Hourai can connect to with `music` (case
  insensitive) in it's name.
