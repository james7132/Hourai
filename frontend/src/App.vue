<template>
  <router-view></router-view>
</template>

<script>
export default {
  name: 'App',
}
</script>

<style lang="scss">
@import "@/scss/_variables.scss";
@import "~bulma";
@import "~buefy/src/scss/buefy";
@import "@/scss/_overrides.scss";

html {
  width: 100%;
  height: 100%;
}

body {
  width: 100%;
  min-height: 100%;
}
</style>
