import Vue from 'vue'
import VueRouter from 'vue-router'

import LandingPage from '@/components/LandingPage/LandingPage.vue'
import GuildConfig from '@/components/GuildConfig.vue'
import NotFoundComponent from '@/components/NotFoundComponent.vue'

Vue.use(VueRouter)

export default new VueRouter({
    mode: 'history',
    routes: [{
            path: "/",
            component: LandingPage
        },
        {
            path: '/guild/:guild_id(\\d+)',
            component: GuildConfig,
            children: []
        },
        {
            path: '*',
            component: NotFoundComponent
        }
    ]
})
