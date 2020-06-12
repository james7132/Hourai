<template>
  <div id="dashboard" class="container">
    <img class="logo" alt="Hourai Logo" src="@/assets/logo.webp">
    <ul v-if="user">
      <li v-for="guild in guilds" v-bind:key="guild.id">
        {{guild.id}}: {{guild.name}}
      </li>
    </ul>
    <div v-else>
      <button @click.prevent="connect">Connect to Discord</button>
    </div>
  </div>
</template>

<script>
export default {
  name: 'Dashboard',
  data() {
    return {
      user: null,
      guilds: []
    }
  },
}
</script>

<style scoped>
</style>
