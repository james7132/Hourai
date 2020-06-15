import Vue from 'vue'
import VueRouter from 'vue-router'

import LandingPage from '@/components/LandingPage/LandingPage.vue'
import GuildSelect from '@/components/Dashboard/GuildSelect.vue'
import Dashboard from '@/components/Dashboard/Dashboard.vue'
import Login from '@/components/Login.vue'
import NotFoundComponent from '@/components/NotFoundComponent.vue'

Vue.use(VueRouter)

export default new VueRouter({
    mode: 'history',
    routes: [{
          path: "/",
          component: LandingPage
      }, {
          path: '/login',
          name: 'login',
          component: Login,
      }, {
          path: '/dash',
          name: 'dashboard',
          component: GuildSelect,
          meta: { requiresAuth: true },
      }, {
          path: '/dash/:guild_id(\\d+)',
          name: 'dashboard-guild',
          component: Dashboard,
          meta: { requiresAuth: true }
      }, {
          path: '*',
          component: NotFoundComponent
      }
    ]
})
