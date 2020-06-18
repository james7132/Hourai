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
        placeholder="Search your servers" icon="search" rounded>
      </b-input>
      <div class="columns is-multiline is-centered is-mobile guild-container">
        <div
          class="column is-1-desktop is-one-fifth-mobile is-one-fifth-tablet"
          v-for="guild in guilds"
          :key="guild.id">
          <b-tooltip v-if="guild.has_bot" class="full-size" :label="guild.name">
            <router-link class="icon-container is-block" :to="getDashLink(guild)">
              <GuildIcon classes="is-size-4" :guild="guild">
              </GuildIcon>
            </router-link>
          </b-tooltip>
          <b-tooltip v-if="!guild.has_bot" class="full-size"
                     :label="`Add bot to ${guild.name}`">
            <a class="icon-container full-size" :href="getInviteLink(guild)">
              <GuildIcon classes="is-size-4" :guild="guild">
                <template slot="overlay">
                  <div class="overlay is-text-center">
                    <b-icon type="is-primary" size="is-large"
                            pack="fa" icon="plus"/>
                  </div>
                </template>
              </GuildIcon>
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

.full-size {
  display: block;
  width: 100%;
  height: 100%;
}

.overlay {
  content: " ";
  z-index: 10;
  display: flex;
  position: absolute;
  justify-content: center;
  align-items: center;
  height: 100%;
  top: 0;
  left: 0;
  right: 0;
  border-radius: 50%;
  opacity: 0;
  background-color: #000;
  transition: .5s ease;
}

.icon-container:hover .overlay {
  opacity: 0.75;
}

.overlay.icon {
  height: 30rem;
  width: 30rem;
}
</style>
