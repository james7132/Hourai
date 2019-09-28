local env = {
  bot_token: ""
  command_prefix: "~"

  database: "sqlite//"
  redis: "redis://redis"

  lavalink: {
    nodes: [ {
        identifier: "MAIN"
        host: "lavalink"
        port: 80
        rest_uri: "http://lavalink:2333"
        region: "us_central"
        password: null
    }]
  }

  reddit: {
    client_id: ""
    client_secret: ""
    username: ""
    password: ""

    user_agent: linux:discord.hourai.reddit:v2.0 (by /u/james7132)
    base_url: https://reddit.com
    fetch_limit: 20
  }

  logging: {
    default: "INFO"
    modules: {
      prawcore: "INFO"
      aioredis: "INFO"
    }
  }
}

{
  // Denote different configurations for different enviroments here.
  prod = env {}
  dev = env {}
}
