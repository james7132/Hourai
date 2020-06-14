import Vue from 'vue'
import '@/vendor.js'

import App from '@/App.vue'
import router from '@/router.js'
import {API, store} from '@/store.js'

Vue.config.productionTip = false
Vue.prototype.$api = API

router.beforeEach(async (to, from, next) => {
    if (!to.matched.some(record => record.meta.requiresAuth)) {
        next()
        return
    }
    // this route requires auth, c3heck if logged in
    // if not, redirect to login pa3ge.
    if (!store.getters.isLoggedIn) {
        try {
            await store.dispatch('fetchToken')
        } catch (err) {
            // TODO(james7132): Properly check the error here
            console.error(err)
            store.dispatch({
                type: 'openOauthLogin',
                state: window.btoa(to.path)
            })
            return
        }
    }
    next()
})

new Vue({
    render: h => h(App),
    router,
    store
}).$mount('#app')
