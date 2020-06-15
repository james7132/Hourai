<template>
  <div id="dashboard">
    <b-loading
      :is-full-page="true"
      :active.sync="isLoading"
      :can-cancel="false"
    ></b-loading>
    <DashboardNavbar v-if="!isLoading" title="Dashboard">l </DashboardNavbar>
    <div class="container" v-if="!isLoading">
      <b-input class="search" v-model="name"
        placeholder="Search your servers" icon="magnify" rounded>
      </b-input>
      <div class="columns is-multiline is-centered is-mobile guild-container">
        <div
          class="column is-1-desktop is-one-fifth-mobile is-one-fifth-tablet"
          v-for="guild in guilds"
          :key="guild.id">
          <b-tooltip class="is-block" :label="guild.name">
            <router-link v-if="guild.has_bot" class="is-block"
                          :to="getDashLink(guild)">
              <GuildIcon class="is-square" :guild="guild">
                <template slot="overlay">
                  Lmao
                </template>
              </GuildIcon>
            </router-link>
            <a v-else class="is-block" :href="getInviteLink(guild)">
              <GuildIcon class="is-square" :guild="guild"> </GuildIcon>
            </a>
          </b-tooltip>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import DashboardNavbar from "./Navbar.vue";
import GuildIcon from "@/components/common/GuildIcon.vue";
import consts from "@/consts.js"

export default {
  name: "Dashboard",
  components: {
    DashboardNavbar,
    GuildIcon
  },
  computed: {
    guilds() {
      let guilds = this.$store.state.guilds.guilds;
      let search = this.name.toLowerCase()
      return Object.values(guilds)
        .filter(g => g.name && g.name.toLowerCase().includes(search))
        .sort((a, b) => !!b.has_bot - !!a.has_bot)
    }
  },
  data() {
    return {
      isLoading: true,
      name: ""
    };
  },
  methods: {
    getDashLink(guild) {
      return {
        name: 'dashboard-guild',
        params: { guild_id: guild.id }
      }
    },
    getInviteLink(guild) {
      return consts.links.bot +
             `&disable_guild_select=true&guild_id=${guild.id}`
    },
  },
  async mounted() {
    await this.$store.dispatch("loadGuilds");
    this.isLoading = false;
  },
  beforeRouteEnter(to, from, next) {
    next(vm => {
      vm.$store.commit('selectGuild', null)
    })
  }
};
</script>

<style scoped>
.search {
  margin-top: 20px;
  margin-bottom: 20px;
  margin-left: 20px;
  margin-right: 20px;
}

.guild-container {
  padding-left: 3%;
  padding-right: 3%;
}
</style>
