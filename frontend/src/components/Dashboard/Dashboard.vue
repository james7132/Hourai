<template>
  <div id="dashboard" class="full-height">
    <b-loading :is-full-page="true"
      :active.sync="isLoading"
      :can-cancel="false"></b-loading>
    <DashboardNavbar v-if="!isLoading"></DashboardNavbar>
    <div id="content" class="container" v-if="!isLoading">
      <div class="columns">
        <div class="column is-2">
          <GuildIcon class="is-square" :guild="selectedGuild"></GuildIcon>
          <DashboardMenu id="menu"></DashboardMenu>
        </div>
        <div class="column">
          <router-view></router-view>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import DashboardNavbar from "./Navbar.vue"
import DashboardMenu from "./Menu.vue"
import GuildIcon  from '@/components/common/GuildIcon.vue'
import { mapGetters } from 'vuex'

export default {
  name: 'Dashboard',
  components: {
    DashboardNavbar,
    DashboardMenu,
    GuildIcon,
  },
  data() {
    return {
      isLoading: true
    }
  },
  computed: {
    ...mapGetters(['selectedGuild'])
  },
  async mounted() {
      await this.$store.dispatch('loadGuilds')
      this.isLoading = false
  },
  beforeRouteEnter(to, from, next) {
    next(vm => {
      vm.$store.commit('selectGuild', to.params.guild_id)
    })
  }
}
</script>

<style scoped>
#content {
  padding-top: 0.75%;
  height: 100%;
}

#guild-icon {
  margin-left: auto;
  margin-right: auto;
}

#menu {
  margin-top: 20px;
}

.full-height {
  display: block;
  height: 100%;
}
</style>
