<template>
  <router-view></router-view>
</template>

<script>
export default {
  name: 'App',
}
</script>

<style lang="scss">
@import "~bulma/sass/utilities/_all";
@import "@/scss/_variables.scss";
@import "~bulma";
@import "@/scss/_overrides.scss";
@import "~buefy/src/scss/buefy";
</style>
