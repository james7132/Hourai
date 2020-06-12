import Vue from 'vue'
import '@/vendor.js'

import App from '@/App.vue'
import router from '@/router.js'
import HouraiApi from '@/api.js'

const HOST = window.location.host
const API_VERSION = 1
const API = new HouraiApi(HOST, API_VERSION)
Vue.prototype.$api = API
Vue.prototype.$auth = API.auth
Vue.config.productionTip = false

router.beforeEach(async (to, from, next) => {
    if (to.matched.some(record => record.meta.requiresAuth)) {
        // this route requires auth, check if logged in
        // if not, redirect to login page.
        if (!API.auth.isLoggedIn()) {
            await API.auth.getAuthToken()
            console.assert(this.$auth.isLoggedIn());
        }
    }
    next()
})

new Vue({
    render: h => h(App),
    router
}).$mount('#app')
