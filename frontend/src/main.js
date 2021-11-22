import Vue, { createApp } from 'vue';
import '@/vendor.js'

import router from '@/router.js'
import {API, store} from '@/store.js'
import App from '@/App.vue'

Vue.prototype.$api = API

createApp(App).use(router).use(store).mount('#app')
