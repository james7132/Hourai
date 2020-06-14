<template>
    <b-navbar wrapper-class="container" shadow>
      <template slot="start">
        <b-navbar-dropdown :label="selectedGuild.name" scrollable=true>
          <NavbarGuildOption
            v-for="guild of guilds"
            :key="guild.id"
            v-bind="guild">
          </NavbarGuildOption>
        </b-navbar-dropdown>
        <b-tooltip label="Add to Server" position="is-bottom">
          <b-navbar-item>
            <b-icon pack="fa" icon="plus"/>
          </b-navbar-item>
        </b-tooltip>
      </template>
      <template slot="end">
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
import NavbarGuildOption from './NavbarGuildOption.vue'
import consts from '@/consts.js'
import { mapGetters } from 'vuex'

export default {
  name: 'DashboardNavbar',
  components: {NavbarSocial, NavbarGuildOption},
  data() {
    return { consts }
  },
  computed: {
    guilds: function() {
      return this.$store.state.guilds
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

<style>
.guild-icon {
  margin-right: 10px;
}
</style>
