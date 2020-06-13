<template>
    <b-navbar shadow>
      <template slot="start">
        <b-navbar-dropdown :label="selectedGuild.name" scrollable=true>
          <b-navbar-item
            v-for="guild in guilds"
            :key="guild.id">
            <figure class="image is-32x32 guild-icon">
              <img class="is-rounded" :src="guild.icon_url">
            </figure>
            {{guild.name}}
          </b-navbar-item>
        </b-navbar-dropdown>
      </template>
      <template slot="end">
          <b-navbar-item tag="div" >
            <div class="buttons">
              <a class="button is-primary" @click="logout">
                <strong>Logout</strong>
              </a>
            </div>
          </b-navbar-item>
      </template>
    </b-navbar>
</template>

<script>
export default {
  name: 'DashboardNavbar',
  props: ["guilds", "selectedGuild"],
  methods: {
    async logout() {
      try {
        await this.$api.auth.logout()
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
