import Vue from 'vue'
import '@/vendor.js'

import App from '@/App.vue'
import router from '@/router.js'

Vue.config.productionTip = false

new Vue({
  render: h => h(App),
  router
}).$mount('#app')
