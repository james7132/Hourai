import Vue from 'vue'
import '@/vendor.js'

import App from '@/App.vue'
import router from '@/router.js'
import HouraiApi from '@/api.js'

const HOST = window.location.host
const API_VERSION = 1
Vue.prototype.$api = new HouraiApi(HOST, API_VERSION)
Vue.config.productionTip = false

new Vue({
  render: h => h(App),
  router
}).$mount('#app')
