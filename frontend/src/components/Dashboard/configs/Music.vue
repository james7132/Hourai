<template>
  <div id="dashboard">
    <b-loading :is-full-page="true"
      :active.sync="isLoading"
      :can-cancel="false"></b-loading>
    <div v-if="!isLoading">
      <DashboardNavbar></DashboardNavbar>
      <div class="columns">
        <div class="column is-2">
          <DashboardMenu></DashboardMenu>
        </div>
        <div class="column">
          <div class="container">
            <router-view></router-view>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import DashboardNavbar from "./Navbar.vue"
import DashboardMenu from "./Menu.vue"

export default {
  name: 'Dashboard',
  components: {
    DashboardNavbar,
    DashboardMenu,
  },
  data() {
    return {
      isLoading: true
    }
  },
  async mounted() {
      await this.$store.dispatch('fetchGuilds')
      this.isLoading = false
  },
}
</script>

<style scoped>
</style>
