local env(suffix) = {
  bot_token: error "Must override bot_token.",
  command_prefix: "~",

  database: "sqlite:////data/hourai.sqlite",
  redis: "redis://redis-" + suffix,

  music: {
    nodes: [{
      identifier: "MAIN",
      host: "lavalink-" + suffix,
      port: 2333,
      rest_uri: "http://lavalink-" + suffix + ":2333",
      region: "us_central",
      password: null,
    }],
  },

  reddit: {
    client_id: "",
    client_secret: "",
    username: "",
    password: "",

    user_agent: "linux:discord.hourai.reddit:v2.0 (by /u/james7132)",
    base_url: "https://reddit.com",
    fetch_limit: "20",
  },

  logging: {
    default: "INFO",
    modules: {
      prawcore: "INFO",
      aioredis: "INFO",
      wavelink: "INFO",
    },
  }
};

{
  // Denote different configurations for different enviroments here.
  prod: env("prod") {
    // Hourai:
    bot_token: "",
  },
  dev: env("dev") {
    // Shanghai:
    bot_token: "",

    logging: {
      default: "DEBUG",
      modules: {
        prawcore: "INFO",
        aioredis: "DEBUG",
        wavelink: "DEBUG",
        discord: "INFO"
      },
    }
  }
}
