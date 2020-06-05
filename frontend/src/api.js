const API_BASE = `https://${window.location.host}/api/v{0}`

class Resource {
    constructor(api, suffix) {
        this.api = api
        this.endpoint = api.prefix + suffix
    }

    raise_if_error(method, response) {
      if (response.ok) return
      const format = "Error: {0} on {1} errored with status {2}"
      throw new Error(msg.format(method, this.endpoint, response.status))
    }

    async get() {
          const response = await fetch(this.endpoint, {
              method: 'GET'
          })
          this.raise_if_error('GET', response)
          return await response.json()
    }

    async post(data) {
        const response = await fetch(this.endpoint, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(data)
        })
        this.raise_if_error('POST', response)
        return await response.json()
    }
}

export default class HouraiApi {
    constructor(api_version) {
        this.prefix = API_BASE.format(api_version)
    }

    guild_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}.json`)
    }

    logging_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/logging.json`)
    }

    auto_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/logging.json`)
    }

    moderation_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/moderation.json`)
    }

    music_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/music.json`)
    }

    announce_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/announce.json`)
    }

    role_config(guild_id) {
        return new Resource(this, `/guild/${guild_id}/role.json`)
    }
}
