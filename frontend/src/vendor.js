import 'es6-promise/auto'

import Vue from 'vue'
import VueRouter from 'vue-router'
import Buefy from 'buefy'
import Vuex from 'vuex'

import { Plugin } from 'vue-fragment'

import { library } from '@fortawesome/fontawesome-svg-core'
import { faTwitter, faDiscord, faGithub } from '@fortawesome/free-brands-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

library.add(faTwitter, faDiscord, faGithub)
Vue.component('font-awesome-icon', FontAwesomeIcon)

import 'buefy/dist/buefy.css'

Vue.use(Buefy, {
  defaultIconComponent: 'font-awesome-icon',
  defaultIconPack: 'fas',
})
Vue.use(VueRouter)
Vue.use(Plugin)
Vue.use(Vuex)
