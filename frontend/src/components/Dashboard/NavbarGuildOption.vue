<template>
  <b-navbar-item>
    <figure class="image is-32x32 guild-icon">
      <img class="is-rounded" :src="imageLink">
    </figure>
    {{guild.name}}
  </b-navbar-item>
</template>

<script>
import consts from '@/consts.js'
export default {
  name: 'DashboardNavbarGuild',
  props: ["guild"],
  computed: {
    imageLink() {
      return consts.discord.guildIcon(this.guild, 32)
    }
  },
}
</script>

<style scoped>
.guild-icon {
  margin-right: 10px;
}
</style>
