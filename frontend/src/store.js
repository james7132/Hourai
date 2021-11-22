import qs from 'qs'
import Vuex from "vuex"
import Api from "@/api.js"
import consts from '@/consts.js'

const API = new Api(window.location.host, 1)

let auth = {
    state: {
        token: null,
    },
    mutations: {
        login(state, token) {
            state.token = token
            API.updateAuth(token)
        },
        logout(state) {
            state.token = null
            API.updateAuth(null)
        },
    },
    getters: {
        isLoggedIn: (state) => !!state.token,
    },
    actions: {
        async fetchToken({commit}) {
            let response = await API.authToken().get()
            commit("login", response.data[consts.auth.token_key])
        },

        async login({commit}, code) {
            let response = await API.oauthLogin().post({code: code})
            commit("login", response.data[consts.auth.token_key])
        },

        async logout({commit}) {
            await API.oauthLogout().post()
            commit("logout")
        },

        openOauthLogin(_, {state=null}) {
            let domain = `${window.location.protocol}//${window.location.host}`
            const endpoint = consts.auth.authorize_endpoint
            const query = qs.stringify({
                client_id: consts.api.clientId,
                response_type: 'code',
                scope: 'identify guilds',
                redirect_uri: `${domain}/login`,
                state: state || undefined
            })
            window.location = `${endpoint}?${query}`
        }
    }
}

let guilds = {
    state: {
        guilds: null,
        selectedGuildId: null,
    },
    mutations: {
        selectGuild(state, guildId) {
            state.selectedGuildId = guildId
        },
        setGuild(state,  guild) {
            if (!state.guilds) {
              state.guilds = {}
            }
            state.guilds[guild.id] = guild
        }
    },
    getters: {
        selectedGuild(state) {
            return state.guilds[state.selectedGuildId]
        },
        getGuild: state => id => {
            return state.guilds[id]
        },
        haveGuildsLoaded(state) {
            return !!state.guilds
        }
    },
    actions: {
        async loadGuilds(ctx) {
            if (ctx.getters.haveGuildsLoaded) return
            await ctx.dispatch('fetchGuilds')
        },
        async fetchGuilds({commit}) {
            let response = await API.guilds().get()
            let guilds = response.data
            guilds.forEach(guild => commit('setGuild', guild))
            //if (guilds.length > 0 && state.selectedGuildId === null) {
                //commit('selectGuild', guilds[0].id)
            //}
        }
    }
}

const store = Vuex.createStore({
    strict: process.env.NODE_ENV !== 'production',
    modules: {
        auth,
        guilds
    }
})
API.store = store

export {
    API,
    store
}
