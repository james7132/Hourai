import Vue from 'vue'
import VueRouter from 'vue-router'

import LandingPage from '@/components/LandingPage/LandingPage.vue'
import Dashboard from '@/components/Dashboard/Dashboard.vue'
import Login from '@/components/Login.vue'
import NotFoundComponent from '@/components/NotFoundComponent.vue'

Vue.use(VueRouter)

export default new VueRouter({
    mode: 'history',
    routes: [{
            path: "/",
            component: LandingPage
        },
        {
            path: '/login',
            component: Login,
        },
        {
            path: '/dash',
            component: Dashboard,
            meta: { requiresAuth: true },
            children: []
        },
        {
            path: '*',
            component: NotFoundComponent
        }
    ]
})
