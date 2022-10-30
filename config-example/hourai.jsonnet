local option = {
  name: error "Must override option name",
  description: error "Must override option description",
  type: error "Must override option type",
};

local subcommand = option {
  type: 1 // SUB_COMMAND
};

local subcommand_group = option {
  type: 2 // SUB_COMMAND_GROUP
};

local string = option {
  type: 3 // STRING
};

local integer = option {
  type: 4 // INTEGER
};

local boolean = option {
  type: 5 // BOOLEAN
};

local user = option {
  type: 6 // USER
};

local channel = option {
  type: 7 // CHANNEL
};

local role = option {
  type: 8 // ROLE
};

local command = {
  name: error "Must override command name",
  description: error "Must override command description",
  options: [],
  version: 1,
};

local commands = [
  command {
    name: "music",
    description: "Music bot related commands",
    options: [subcommand {
      name: "play",
      description: "Adds a song or playlist in a voice channel.",
      options: [string {
        name: "query",
        description: "The query or URL of the song to add to the queue.",
      }],
    },
    subcommand {
      name: "pause",
      description: "Pauses the currently playing song. Only usable by DJs.",
    },
    subcommand {
      name: "stop",
      description: "Stops the currently playing song and clears the queue. Only usable by DJs.",
    },
    subcommand {
      name: "remove",
      description: "",
      options: [
      integer {
        name: "position",
        description: "Position of the song to be removed.",
      },
      boolean {
        name: "all",
        description: "If set, remove all of your remaining songs from the queue.",
      }]
    },
    subcommand {
      name: "skip",
      description: "Votes to skip the currently playing song.",
      options: [boolean {
        name: "force",
        description: "Optional: Forcibly skips the current song, regardless of how many votes there currently are. Only usable by DJs.",
      }]
    },
    subcommand {
      name: "volume",
      description: "Gets or sets the volume for the music bot.",
      options: [integer {
        name: "force",
        description: "The volume to set the music bot to. Must be used by a DJ.",
      }]
    },
    subcommand {
      name: "nowplaying",
      description: "Shows what's currently playing in the music bot.",
    },
    subcommand {
      name: "queue",
      description: "Shows what's currently queued to play in the musicbot.",
    }],
  },

  command {
    name: "role",
    description: "Changes the roles of a given user. Requires Manage Roles.",
    options: [subcommand {
      name: "add",
      description: "Adds a role to one or more users.",
      options: [role {
        name: "role",
        description: "The role to add to the user.",
        required: true,
      }, string {
        name: "duration",
        description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
      }] + [user {
        name: "user_" + x,
        description: "The user to add the role to.",
      }
      for x in std.range(1, 23)]
    }, subcommand {
      name: "remove",
      description: "Removes a role to one or more users.",
      options: [role {
        name: "role",
        description: "The role to remove from the user.",
        required: true,
      }, string {
        name: "duration",
        description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
      }] + [user {
        name: "user_" + x,
        description: "The user to add the role to.",
      }
      for x in std.range(1, 23)]
    }]
  },

  command {
    name: "ban",
    description: "Bans a user from the server. Requires Ban Members.",
    options: [string {
      name: "reason",
      description: "Optional: the reason for the ban.",
    }, string {
      name: "duration",
      description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
    }, boolean {
      name: "soft",
      description: "Optional: If true, immediately unbans after banning. Useful for mass deleting messages without permanent changes."
    }] + [user {
      name: "user_" + x,
      description: "The user to ban.",
    }
    for x in std.range(1, 22)]
  },

  command {
    name: "kick",
    description: "Kicks a user from the server. Requires Kick Members.",
    options: [string {
      name: "reason",
      description: "Optional: the reason for the kick.",
    }] + [user {
      name: "user_" + x,
      description: "The user to kick from the server.",
    }
    for x in std.range(1, 24)]
  },

  command {
    name: "timeout",
    description: "Times out user(s) from the server. Requires Moderate Members.",
    options: [string {
      name: "reason",
      description: "Optional: the reason for the timeout.",
    }, string {
      name: "duration",
      description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
    }] + [user {
      name: "user_" + x,
      description: "The user to time out.",
    }
    for x in std.range(1, 22)]
  },

  command {
    name: "move",
    description: "Moves all of the users in a voice channel to another. Requires Move Members.",
    options: [channel {
      name: "src",
      description: "The source channel to move from.",
      required: true,
    }, channel {
      name: "dst",
      description: "The destination channel to move to.",
      required: true,
    }]
  },

  command {
    name: "prune",
    description: "Bulk deletes messages up to 14 days old. Requires Manage Messages.",
    options: [integer {
      name: "count",
      description: "Optional: The number of messages to check. If not set, defaults to 100.",
    }, user {
      name: "user",
      description: "Optional: If set, only deletes messages from the specified user.",
    }, string {
      name: "match",
      description: "Optional: If set, only deletes messages that contain the pattern. Supports matching via regular expressions.",
    }, boolean {
      name: "embed",
      description: "Optional: If true, only deletes messages from embeds or attachments.",
    }, boolean {
      name: "mention",
      description: "Optional: If true, only deletes messages that mention users or roles.",
    }, boolean {
      name: "bot",
      description: "Optional: If true, only deletes messages from bots.",
    }]
  },

  command {
    name: "mute",
    description: "Server mutes a user from the server. Requires Mute Members.",
    options: [string {
      name: "reason",
      description: "Optional: reason for the mute.",
    }, string {
      name: "duration",
      description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
    }] + [user {
      name: "user_" + x,
      description: "The user to mute.",
    }
    for x in std.range(1, 23)]
  },

  command {
    name: "deafen",
    description: "Server deafens from the server. Requires Deafen Members.",
    options: [string {
      name: "reason",
      description: "Optional: reason for the deafen.",
    }, string {
      name: "duration",
      description: "Optional: sets when to undo the change. (i.e. 6d for 6 days, 2h for 2 hours.)",
    }] + [user {
      name: "user_" + x,
      description: "The user to deafen.",
    }
    for x in std.range(1, 24)]
  },

  command {
    name: "choose",
    description: "Randomly chooses between several different choices",
    options: [string {
      name: "choice_" + x,
      description: "A choice to choose from.",
    }
    for x in std.range(1, 24)]
  },

  command {
    name: "remindme",
    description: "Schedules a reminder up to one year in the future. The bot will
    send you a DM with a reminder at the appropriate time.",
    options: [string {
      name: "time",
      description: "Required: sets when to send the reminder. (i.e. 6d for 6 days, 2h for 2 hours.",
      required: true
    }, string {
      name: "reminder",
      description: "Required: What should you be reminded of?",
      required: true
    }]
  },

  command {
    name: "ping",
    description: "Pings one random online moderator to pay attention to the current channel.",
    options: [subcommand {
      name: "mod",
      description: "Pings one random online moderator to pay attention to the current channel.",
      options: [string {
        name: "reason",
        description: "Optional: a reason for contacting the mod.",
      }]
    }, subcommand {
      name: "event",
      description: "Pings all of the subscribed users to ongoing active scheduled events on the server.",
      options: []
    }]
  },

  command {
    name: "info",
    description: "Provides detailed information about things on Discord.",
    options: [subcommand {
      name: "user",
      description: "Provides detailed information about a user.",
      options: [user {
        name: "user",
        description: "The user to find information on",
        required: true
      }]
    }]
  },

  command {
    name: "verification",
    description: "",
    options: [subcommand {
      name: "verify",
      description: "Required: the user that is being inspected.",
      options: [user {
        name: "user",
        description: "The user to run verification manually on.",
        required: true,
      }]
    }, subcommand {
      name: "setup",
      description: "Sets up verification on the current server.",
      options: [role {
        name: "role",
        description: "Optional: The verification role that is to be given out upon verification.",
      }]
    }, subcommand {
      name: "propagate",
      description: "Distributes the verification role to all users currently on the server.",
    }, subcommand {
      name: "purge",
      description: "Kicks all users that have not been verified within a specified timeframe from the server. Requires Kick Members.",
      options: [string {
        name: "lookback",
        description: "Required: Maximum unverified time since joining the server.",
        required: true,
      }]
    }]
  },

  command {
    name: "escalate",
    description: "Progressive tracked moderation.",
    options: [subcommand {
      name: "up",
      description: "Required: the user that is being inspected.",
      options: [string {
        name: "reason",
        description: "The reason to escalate the user for.",
        required: true,
      }, integer {
        name: "amount",
        description: "Defaults to 1. Number of levels to go up.",
      }] + [user {
        name: "user_" + x,
        description: "The user to kick from the server.",
      }
      for x in std.range(1, 24)]
    }, subcommand {
      name: "down",
      description: "Sets up verification on the current server.",
      options: [string {
        name: "reason",
        description: "The reason to escalate the user for.",
        required: true,
      }, integer {
        name: "amount",
        description: "Defaults to 1. Number of levels to go down.",
      }] + [user {
        name: "user_" + x,
        description: "The user to kick from the server.",
      }
      for x in std.range(1, 24)]
    }, subcommand {
      name: "history",
      description: "Distributes the verification role to all users currently on the server.",
      options: [user {
        name: "user",
        description: "The user to kick from the server.",
        required: true,
      }]
    }],
  },

  command {
    name: "config",
    description: "Configure the bot.",
    options: [subcommand_group {
      name: "announce",
      description: "Enables/disables announcements in this channel.",
      options: [subcommand {
        name: "join",
        description: "Enables/disables announcements of user joins in this channel.",
        options: [],
      }, subcommand {
        name: "leave",
        description: "Enables/disables announcements of user leaves in this channel.",
        options: [],
      }, subcommand {
        name: "ban",
        description: "Enables/disables announcements of user bans in this channel.",
        options: [],
      }, subcommand {
        name: "voice",
        description: "Enables/disables announcements of voice channel changes in this channel.",
        options: [],
      }],
    }, subcommand_group {
      name: "log",
      description: "Enables/disables logging of messsages for this server.",
      options: [subcommand {
        name: "edited",
        description: "Enables/disables announcements of user joins in this channel.",
        options: [],
      }, subcommand {
        name: "deleted",
        description: "Enables/disables announcements of user leaves in this channel.",
        options: [],
      }],
    }, subcommand_group {
      name: "set",
      description: "Designate specific roles/channels for the bot.",
      options: [subcommand {
        name: "dj",
        description: "Enables/disables announcements of user joins in this channel.",
        options: [role {
          name: "role",
          description: "The new DJ role. All roles above it will have DJ perms.",
          required: true,
        }],
      }, subcommand {
        name: "modlog",
        description: "Enables/disables announcements of user leaves in this channel.",
        options: [channel {
          name: "channel",
          description: "The modlog channel.",
          required: true,
        }],
      }],
    }, subcommand_group {
      name: "reddit",
      description: "Changes reddit feeds in the current channel.",
      options: [subcommand {
        name: "add",
        description: "Adds a subreddit feed to the current channel.",
        options: [string {
          name: "subreddit",
          description: "The subreddit to add to the channel.",
          required: true,
        }],
      }, subcommand {
        name: "remove",
        description: "Removes a subreddit feed to the current channel.",
        options: [string {
          name: "subreddit",
          description: "The subreddit to remove from the channel.",
          required: true,
        }],
      }, subcommand {
        name: "list",
        description: "Lists all subreddit feeds in the current channel.",
        options: [],
      }],
    }],
  },
];

local env(suffix) = {
  description: "The world's most advanced security and moderation bot for Discord.",
  command_prefix: "~",
  activity: "Use ~help, https://hourai.gg",
  list_directory: "/opt/lists",
  use_uv_loop: true,

  local databases = {
    local database_params = {
      connector: "postgresql",
      dialect: "psycopg2",
      user: "hourai",
      password: "ddDa",
      host: "postgres",
      database: "hourai",

      connection_string: "%s+%s://%s:%s@%s/%s" % [
      ],
    },

    postgres: database_params,
  },

  database: databases.postgres.connection_string,
  redis: "redis://redis",
  commands: commands,

  web: {
    port: 8080
  },

  metrics: {
    port: 9090
  },

  music: {
    nodes: [{
      identifier: "ddDa",
      host: "lavalink",
      port: 2333,
      rest_uri: "http://lavalink",
      region: "europe",
      password: "ddDa",
    }],
  },

  discord: {
      application_id: error "Must override application_id",
      client_id: error "Must override client_id",
      client_secret: error "Must override client_secret",
      bot_token: error "Must override bot_token.",
      proxy: null,
      gateway_queue: null,
  },

  reddit: {
    client_id: "ddDa",
    client_secret: "ddDa",
    user_agent: "linux:discord.hourai.reddit:v2.0 (by /u/james7132)",
  },

  logging: {
    default: "INFO",
    access_log_format: '"%r" %s %b %Tf "%{Referer}i" "%{User-Agent}i"',
    modules: {
      prawcore: "INFO",
      aioredis: "INFO",
      wavelink: "INFO",
    },
  },

  third_party: {
    discord_bots_token: "",
    discord_boats_token: "",
    top_gg_token: "",
  },

  webhooks: {
    bot_log: "",
  },

  disabled_extensions: []
};

{
  // Denote different configurations for different enviroments here.
  prod: env("prod") {
    // Hourai:
    discord: {
      application_id: 0,
      client_id: "ddDa",
      client_secret: "ddDa",
      redirect_uri: "https://hourai.gg/login",
      bot_token: "ddDa",
      proxy: "http://http-proxy/"
      gateway-queue: "http://gateway-queue/",
    },
  },
  dev: env("dev") {
    // Shanghai:
    discord: {
      application_id: 0,
      client_id: "ddDa",
      client_secret: "ddDa",
      redirect_uri: "http://localhost:8080/login",
      bot_token: "ddDa",
      proxy: null,
      gateway-queue: null,
    },

    logging: {
      default: "DEBUG",
      modules: {
        prawcore: "INFO",
        aioredis: "DEBUG",
        wavelink: "DEBUG",
        discord: "INFO"
      },
    },

    disabled_extensions: [
      'hourai.extensions.feeds'
    ]
  }

}
