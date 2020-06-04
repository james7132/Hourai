<template>
  <div class="container">
    <b-navbar>
      <template slot="start">
        <b-tooltip
           v-for="social in social_media"
           v-bind:key="social.id"
           v-bind:label="social.tooltip"
           position="is-bottom">
          <b-navbar-item
             v-bind:href="social.url">
            <b-icon pack="fab" v-bind:icon="social.icon"/>
          </b-navbar-item>
        </b-tooltip>
      </template>
      <template slot="end">
        <b-navbar-item href="#">Docs</b-navbar-item>
        <b-navbar-item href="#">Log in</b-navbar-item>
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
        id: "discord",
        icon: "discord",
        url: "https://discord.gg/UydKWHX",
        tooltip: "Join the community Discord server!"
      }, {
        id: "twitter",
        icon: "twitter",
        url: "https://twitter.com/hourai_gg",
        tooltip: "Twitter"
      }]
    }
  }
}
</script>

<style>
</style>
