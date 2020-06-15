<template>
  <figure class="image">
    <img v-if="guild.icon" class="is-rounded" :src="imageLink">
    <slot #overlay></slot>
  </figure>
</template>

<script>
import consts from '@/consts.js'
export default {
  name: 'GuildIcon',
  props: {
    guild: {
      type: Object,
      required: true
    },
    size: {
      type: Number,
      default: 1024
    }
  },
  computed: {
    imageLink() {
      return consts.discord.guildIcon(this.guild, this.iconSize)
    }
  },
}
</script>
