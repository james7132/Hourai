const apiConfig = {
    production: {
        protocol: 'https:',
        clientId: "208460637368614913"
    },
    development: {
        protocol: window.location.protocol,
        clientId: "280108190459232268",
    }
} [process.env.NODE_ENV]

const oauthLink = "https://discord.com/api/oauth2/authorize"

export default {
  links: {
    status: "https://status.hourai.gg/",
    docs: "https://docs.hourai.gg/",
    bot: `${oauthLink}?client_id=${apiConfig.clientId}&scope=bot%20applications.commands`
  },
  auth: {
    token_key: "access_token",
    authorize_endpoint: oauthLink
  },
  api: apiConfig,
  discord: {
    guildIcon(guild, size=1024) {
      return `https://cdn.discordapp.com/icons/${guild.id}/${guild.icon}.webp?size=${size}`
    }
  }
}
