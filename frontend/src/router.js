import Vue from 'vue'
import VueRouter from 'vue-router'

import { store } from '@/store.js'
import utils from '@/utils.js'

import LandingPage from '@/components/LandingPage/LandingPage.vue'
import GuildSelect from '@/components/Dashboard/GuildSelect.vue'
import Dashboard from '@/components/Dashboard/Dashboard.vue'
import GuildViews from '@/components/Dashboard/configs/router.js'
import Login from '@/components/Login.vue'
import NotFoundComponent from '@/components/NotFoundComponent.vue'

Vue.use(VueRouter)

let routes = [{
    path: "/",
    component: LandingPage,
}, {
    path: '/login',
    name: 'login',
    component: Login,
    meta: {
        title: 'Login'
    }
}, {
    path: '/dash',
    name: 'dashboard',
    component: GuildSelect,
    meta: {
        requiresAuth: true,
        title: 'Dashboard'
    },
}, {
    path: '/dash/:guild_id(\\d+)',
    name: 'dashboard-guild',
    component: Dashboard,
    meta: {
        requiresAuth: true,
        async title(to) {
          await store.dispatch('loadGuilds')
          let guildId = to.params.guild_id
          let name = store.getters.getGuild(guildId).name
          if (to.name) {
            return `${name}: ${utils.titleCase(to.name)}`
          }
          return name
        }
    },
    children: GuildViews.getRouterEntries(),
    redirect(to) {
        return {
            name: 'overview',
            params: to.params
        }
    }
}, {
    path: '*',
    component: NotFoundComponent
}]

let router = new VueRouter({
    mode: 'history',
    routes
})

async function authenticationCheck(to) {
    if (!to.matched.some(record => record.meta.requiresAuth)) {
        return true
    } else if (!store.getters.isLoggedIn) {
        // this route requires auth, check if logged in
        // if not, redirect to login page.
        try {
            await store.dispatch('fetchToken')
        } catch (err) {
            // TODO(james7132): Properly check the error here
            console.error(err)
            store.dispatch({
                type: 'openOauthLogin',
                state: window.btoa(to.path)
            })
            return false
        }
    }
    return true
}

async function getTitle(title, to) {
    if (typeof title === 'function') {
        title = title(to)
        console.log(title)
        if (title !== null && typeof title.then === 'function') {
          title = await title
          console.log(title)
        }
    }
    if (typeof title === 'string') {
        title = {
          name: title,
          excludeSuffix: false
        }
    }
    if (typeof title === 'object') {
        if (!title.excludeSuffix && title.name) {
            title = `${title.name} | Hourai`
        } else if (!title.name) {
            title = "Hourai"
        }
    }
    return title
}

function findNearestRoute(route, criteria) {
    return route.matched.slice().reverse().find(criteria)
}

// Update meta tags and doc title
async function updateMeta(to) {
    const attribute = "data-vue-router-controlled"
    const nearestWithTitle = findNearestRoute(to, r => r.meta.title)

    const nearestWithMeta = findNearestRoute(to, r => r.meta.metaTags)

    if (nearestWithTitle) {
        document.title = await getTitle(nearestWithTitle.meta.title, to)
    }

    Array.from(document.querySelectorAll(`[${attribute}]`))
        .map(el => el.parentNode.removeChild(el))

    if (!nearestWithMeta) return

    nearestWithMeta.meta.metaTags.map(tagDef => {
        const tag = document.createElement('meta')
        Object.keys(tagDef).forEach(key => {
            tag.setAttribute(key, tagDef[key])
        })
        tag.setAttribute(attribute, '')
        return tag
    }).forEach(tag => document.head.appendChild(tag))
}

// This callback runs before every route change, including on page load.
router.beforeEach(async (to, from, next) => {
    if (!(await authenticationCheck(to))) return
    await updateMeta(to)
    next()
})

export default router
