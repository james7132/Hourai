import Vue, { createApp } from 'vue';
import '@/vendor.js'

import {API, store} from '@/store'
import App from '@/App.vue'
import router from '@/router'

Vue.prototype.$api = API

createApp(App).use(store)
  .use(router)
  .use(store)
  .mount('#app')
