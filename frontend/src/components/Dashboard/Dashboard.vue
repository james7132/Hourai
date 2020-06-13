<template>
  <div id="dashboard">
    <DashboardNavbar :guilds="guilds" :selectedGuild="selectedGuild">
    </DashboardNavbar>
    <img class="logo" alt="Hourai Logo" src="@/assets/logo.webp">
  </div>
</template>

<script>
import DashboardNavbar from "./Navbar.vue"

export default {
  name: 'Dashboard',
  components: {DashboardNavbar},
  data() {
    return {
      user: null,
      guilds: [],
      selectedGuild: null
    }
  },
  mounted() {
      this.refreshGuilds()
  },
  methods: {
    async refreshGuilds() {
      let response = await this.$api.guilds().get()
      let guilds = await response.json()
      for (let guild of guilds) {
        guild.icon = `https://cdn.discordapp.com/icons/${guild.id}/${guild.icon}.png?size=128`
        this.guilds.push(guild)
      }
      if (this.selectedGuild === null && guilds.length > 0) {
        this.selectedGuild = guilds[0]
      }
    }
  }
}
</script>

<style scoped>
</style>
