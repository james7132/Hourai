local env(suffix) = {
  command_prefix: "~",
  activity: "https://hourai.gg",
  list_directory: "/opt/lists",

  local databases = {
    local database_params = {
      connector: "postgresql",
      dialect: "psycopg2",
      user: "hourai",
      password: "ddDa",
      host: "postgres",
      database: "hourai",

      connection_string: "%s+%s://%s:%s@%s/%s" % [
        self.connector, self.dialect, self.user, self.password, self.host,
        self.database
      ],
    },

    postgres: database_params,
  },

  database: databases.postgres.connection_string,
  redis: "redis://redis",

  web: {
    port: 8080
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
      client_id: error "Must override client_id",
      client_secret: error "Must override client_secret",
      bot_token: error "Must override bot_token.",
  },

  reddit: {
    client_id: "ddDa",
    client_secret: "ddDa",
    username: "ddDa",
    password: "ddDa",

    user_agent: "linux:discord.hourai.reddit:v2.0 (by /u/james7132)",
    base_url: "https://reddit.com",
    fetch_limit: "20",
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
      client_id: "ddDa",
      client_secret: "ddDa",
      redirect_uri: "https://hourai.gg/login",
      bot_token: "ddDa",
    },
  },
  dev: env("dev") {
    // Shanghai:
    discord: {
      client_id: "ddDa",
      client_secret: "ddDa",
      redirect_uri: "http://localhost:8080/login",
      bot_token: "ddDa",
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
