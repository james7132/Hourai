<template>
    <b-navbar wrapper-class="container" shadow>
      <template slot="start">
        <b-navbar-item tag="router-link" to="/dash" v-if="selectedGuild">
          <b-icon pack="fa" icon="arrow-left"/>
        </b-navbar-item>
        <b-navbar-item v-if="!selectedGuild">
          {{title}}
        </b-navbar-item>
        <b-navbar-item v-if="selectedGuild">
          {{selectedGuild.name}}
        </b-navbar-item>
      </template>
      <template slot="end">
        <b-tooltip label="Add to Server" position="is-bottom">
          <b-navbar-item>
            <b-icon pack="fa" icon="plus"/>
          </b-navbar-item>
        </b-tooltip>
        <b-tooltip label="Documentation" position="is-bottom">
          <b-navbar-item :href="consts.links.docs">
            <b-icon pack="fas" icon="question-circle"/>
          </b-navbar-item>
        </b-tooltip>
        <NavbarSocial></NavbarSocial>
        <b-tooltip label="Logout" position="is-bottom">
          <b-navbar-item  @click="logout">
            <b-icon pack="fas" icon="sign-out-alt"/>
          </b-navbar-item>
        </b-tooltip>
      </template>
    </b-navbar>
</template>

<script>
import NavbarSocial from '@/components/common/NavbarSocial.vue'
import consts from '@/consts.js'
import { mapGetters } from 'vuex'

export default {
  name: 'DashboardNavbar',
  components: {NavbarSocial},
  props: {
    title: {
      type: String,
      default: "Dashboard"
    }
  },
  data() {
    return { consts }
  },
  computed: {
    guilds: function() {
       return this.$store.state.guilds.guilds
    },
    ...mapGetters(['selectedGuild'])
  },
  methods: {
    async logout() {
      try {
        await this.$store.dispatch('logout')
      } finally {
        this.$router.push('/')
      }
    },
  }
}
</script>
