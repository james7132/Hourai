<template>
  <div class="container">
    <b-navbar>
      <template slot="start">
        <b-tooltip
           v-for="icon in social_media"
           v-bind:key="icon.icon"
           v-bind:label="icon.tooltip"
           position="is-bottom">
          <b-navbar-item v-bind:href="icon.url">
            <b-icon pack="fab" v-bind:icon="icon.icon"/>
          </b-navbar-item>
        </b-tooltip>
      </template>
      <template slot="end">
        <b-navbar-item
         v-for="link in links"
         v-bind:key="link.id"
         v-bind:href="link.url">
          {{link.text}}
        </b-navbar-item>
        <b-navbar-item tag="router-link" to="/dash">
          Dashboard
        </b-navbar-item>
      </template>
    </b-navbar>
  </div>
</template>

<script>
export default {
  name: 'LandingPageNavBar',
  data: function() {
    return {
      social_media: [{
        icon: "discord",
        url: "https://discord.gg/UydKWHX",
        tooltip: "Support Discord Server"
      }, {
        icon: "github",
        url: "https://github.com/james7132/Hourai",
        tooltip: "GitHub Repo"
      }, {
        icon: "twitter",
        url: "https://twitter.com/hourai_gg",
        tooltip: "Twitter"
      }],
      links: [{
        id: "docs",
        url: "https://docs.hourai.gg",
        text: "Documentation"
      }, {
        id: "status",
        url: "https://status.hourai.gg",
        text: "Status"
      }]
    }
  }
}
</script>

<style>
</style>
