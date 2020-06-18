<template>
  <fragment>
    <b-tooltip
       v-for="icon in social_media"
       v-bind:key="icon.icon"
       v-bind:label="icon.tooltip"
       position="is-bottom">
      <b-navbar-item v-bind:href="icon.url">
        <b-icon pack="fab" v-bind:icon="icon.icon"/>
      </b-navbar-item>
    </b-tooltip>
  </fragment>
</template>

<script>
export default {
  name: 'NavbarSocial',
  data() {
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
      }]
    }
  }
}
</script>

<style>
</style>
