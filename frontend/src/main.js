import Vue from 'vue'
import '@/vendor.js'

import App from '@/App.vue'
import router from '@/router.js'
import {API, store} from '@/store.js'

Vue.config.productionTip = false
Vue.prototype.$api = API

new Vue({
    render: h => h(App),
    router,
    store
}).$mount('#app')
