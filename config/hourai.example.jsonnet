local env(suffix) = {
  command_prefix: "~",
  activity: "https://hourai.gg",

  local databases = {
    local database_params = {
      connector: "postgresql",
      dialect: "psycopg2",
      user: "hourai",
      password: "",
      host: "postgres-" + suffix,
      database: "hourai",

      connection_string: "%s+%s://%s:%s@%s/%s" % [
        self.connector, self.dialect, self.user, self.password, self.host,
        self.database
      ],
    },

    postgres: database_params,
  },

  database: databases.postgres.connection_string,
  redis: "redis://redis-" + suffix,

  web: {
    port: 8080
  }

  music: {
    nodes: [{
      identifier: "EUROPE",
      host: "lavalink-" + suffix,
      port: 2333,
      rest_uri: "http://lavalink-" + suffix + ":2333",
      region: "europe",
      password: "",
    }],
  },

  discord: {
      client_id: 0,
      client_secret: 0,
      bot_token: error "Must override bot_token.",
      scopes: [],
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
  },

  disabled_extensions: []
};

{
  // Denote different configurations for different enviroments here.
  prod: env("prod"),
  dev: env("dev"),
}
