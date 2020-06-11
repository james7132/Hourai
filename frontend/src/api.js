
class Resource {
    constructor(api, suffix, supported_methods=null) {
        this.api = api
        this.endpoint = api.prefix + suffix
        this.supported_methods = supported_methods
    }

    raise_if_error(method, response) {
        if (response.ok) return
        const msg = "Error: {0} on {1} errored with status {2}"
        throw new Error(msg.format(method, this.endpoint, response.status))
    }

    check_supported_methods(method) {
        let supported = this.supported_methods === null ||
                    this.supported_methods.includes(method)
        if (!supported) {
          throw `Endpoint "${this.endpoint}" does not support the HTTP ` +
                `method ${method}`
        }
    }

    async get() {
          this.check_supported_methods('GET')
          const response = await fetch(this.endpoint, {
              method: 'GET'
          })
          this.raise_if_error('GET', response)
          return await response.json()
    }

    async post(data) {
        this.check_supported_methods('POST')
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
    constructor(host, version) {
        let protocol = {
          'development': window.location.protocol,
          'production': 'https',
        }[process.env.NODE_ENV]
        this.prefix = `${protocol}//${host}/api/v${version}`
        console.log(this.prefix)
    }

    bot_status() {
        return new Resource(this, `/bot/status`, ['GET'])
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
