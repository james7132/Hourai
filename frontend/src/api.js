import qs from 'qs'

const AUTH_TOKEN_KEY = 'access_token'

class DiscordAuth {
    constructor({
        api,
        clientId
    }) {
        this.api = api
        this.clientId = clientId
        this.authToken = null
    }

    isLoggedIn() {
        return this.authToken !== null &&
            typeof this.authToken !== 'undefined'
    }

    async getAuthToken() {
        if (this.authToken === null) {
            this.authToken = await this.fetchAuthToken()
        }
        return this.authToken
    }

    async fetchAuthToken() {
        let response = await this.api.authToken().get()
        let data = await response.json()
        return data[AUTH_TOKEN_KEY]
    }

    async login(authCode) {
        let response = await this.api.oauthLogin().post({
            code: authCode
        })
        let data = await response.json()
        this.authToken = data[AUTH_TOKEN_KEY]
        return this.authToken
    }

    async logout() {
        this.authToken = null
        await this.api.oauthLogout().post()
    }

    openOauthLogin({
        state = null
    }) {
        const AUTHORIZE_URL = "https://discord.com/api/oauth2/authorize"
        let params = {
            // TODO(james7132): Use state to navigate to the right location
            client_id: this.clientId,
            response_type: 'code',
            scope: 'guilds',
            redirect_uri: `${this.api.domain}/login`,
            state: state || undefined
        }
        window.location = `${AUTHORIZE_URL}?${qs.stringify(params)}`
    }
}

class Resource {
    constructor({
        api,
        endpoint,
        supportedMethods,
        requiresAuth
    }) {
        this.api = api
        this.endpoint = endpoint
        this.supportedMethods = supportedMethods || null
        this.requiresAuth = true
        if (typeof requiresAuth !== 'undefined') {
            this.requiresAuth = requiresAuth
        }
    }

    raiseIfError(method, response) {
        if (response.ok) return
        const msg = `Error: ${method} on ${this.endpoint} errored with status ${response.status}`
        throw new Error(msg)
    }

    checkSupportedMethods(method) {
        let supported = this.supportedMethods === null ||
            this.supportedMethods.includes(method)
        if (!supported) {
            throw `Endpoint "${this.endpoint}" does not support the HTTP ` +
                `method ${method}`
        }
    }

    async get() {
        return await this.fetch('GET', {
          headers: new Headers(await this.getHeaders())
        })
    }

    async post(data = null, params = {}) {
        await this.addHeaders(params)
        params.body = (data === null) ? undefined : JSON.stringify(data)
        return await this.fetch('POST', params)
    }

    async fetch(method, params = {}) {
        this.checkSupportedMethods(method)
        let response = await fetch(this.endpoint, {
            method: method,
            ...params
        })
        this.raiseIfError(method, response)
        return response
    }

    async addHeaders(params) {
      let additionalHeaders = await this.getHeaders()
      if (!params.headers) {
        params.headers = new Headers()
      }
      for (let prop in additionalHeaders) {
        params.headers.append(prop, additionalHeaders[prop])
      }
    }

    async getHeaders() {
        let headers = {}
        if (this.requiresAuth) {
            const token = await this.api.auth.getAuthToken()
            headers['Authorization'] = 'Bearer ' + token
        }
        console.log(`PATH: ${this.endpoint} AUTH: ${this.requiresAuth} HEADERS: ${JSON.stringify(headers)}`)
        return headers
    }
}

export default class HouraiApi {
    constructor(host, version) {
        let config = {
            production: {
                protocol: 'https:',
                clientId: "208460637368614913"
            },
            development: {
                protocol: window.location.protocol,
                clientId: "280108190459232268",
            }
        } [process.env.NODE_ENV]
        this.domain = `${config.protocol}//${host}`
        this.prefix = `${this.domain}/api/v${version}`
        this.auth = new DiscordAuth({
            api: this,
            clientId: config.clientId
        })
    }

    createResource(endpoint, params = {}) {
        return new Resource({
            api: this,
            endpoint: this.prefix + endpoint,
            ...params
        })
    }

    bot_status() {
        return this.createResource('/bot/status', {
            requiresAuth: false,
            supportedMethods: ['GET']
        })
    }

    authToken() {
        return this.createResource('/oauth/discord/refresh', {
            requiresAuth: false,
            supportedMethods: ['GET']
        })
    }

    oauthLogin() {
        return this.createResource('/oauth/discord/token', {
            requiresAuth: false,
            supportedMethods: ['POST']
        })
    }

    oauthLogout() {
        return this.createResource('/oauth/discord/logout', {
            requiresAuth: false,
            supportedMethods: ['POST']
        })
    }

    me() {
        return this.createResource('/users/@me', {
            supportedMethods: ['GET']
        })
    }

    guilds() {
        return this.createResource('/users/@me/guilds', {
            supportedMethods: ['GET']
        })
    }

    guildConfig(guildId) {
        return this.createResource(`/guild/${guildId}`)
    }

    loggingConfig(guildId) {
        return this.createResource(`/guild/${guildId}/logging.json`)
    }

    autoConfig(guildId) {
        return this.createResource(`/guild/${guildId}/auto.json`)
    }

    moderationConfig(guildId) {
        return new Resource(this, `/guild/${guildId}/moderation.json`)
    }

    musicConfig(guildId) {
        return this.createResource(`/guild/${guildId}/music.json`)
    }

    announceConfig(guildId) {
        return this.createResource(`/guild/${guildId}/announce.json`)
    }

    roleConfig(guildId) {
        return this.createResource(`/guild/${guildId}/role.json`)
    }
}
