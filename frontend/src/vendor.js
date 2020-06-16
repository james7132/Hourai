import 'es6-promise/auto'

import Vue from 'vue'
import VueRouter from 'vue-router'
import Buefy from 'buefy'
import Vuex from 'vuex'

import Axios from 'axios'

import { Plugin } from 'vue-fragment'
import { library } from '@fortawesome/fontawesome-svg-core'
import { faTwitter, faDiscord, faGithub } from '@fortawesome/free-brands-svg-icons'
import { faPlus,faSignOutAlt, faQuestionCircle, faArrowLeft, faSearch } from '@fortawesome/free-solid-svg-icons'
import { faQuestionCircle as farQuestionCircle } from '@fortawesome/free-regular-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

library.add(faTwitter, faDiscord, faGithub)
library.add(faSignOutAlt, faPlus, faQuestionCircle, farQuestionCircle,
            faArrowLeft, faSearch)
Vue.component('font-awesome-icon', FontAwesomeIcon)

Vue.prototype.$http = Axios
Vue.use(Buefy, {
  defaultIconComponent: 'font-awesome-icon',
  defaultIconPack: 'fas',
})
Vue.use(VueRouter)
Vue.use(Plugin)
Vue.use(Vuex)
