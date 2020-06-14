<template>
  <b-navbar-item>
    <figure class="image is-32x32 guild-icon">
      <img class="is-rounded" :src="imageLink">
    </figure>
    {{guild.name}}
  </b-navbar-item>
</template>

<script>
export default {
  name: 'DashboardNavbarGuild',
  props: ["id", "icon", "name"],
  computed: {
    imageLink() {
      const url = 'https://cdn.discordapp.com/icons/{0}/{1}.png?size=32'
      return url.format(this.id, this.icon)
    }
  },
}
</script>

<style>
.guild-icon {
  margin-right: 10px;
}
</style>
