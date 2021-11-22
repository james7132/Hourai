<template>
  <fragment>
    <figure v-if="guild.icon" :class="allImageClasses" v-bind="$attrs">
      <img class="is-rounded" :src="imageLink">
      <slot></slot>
    </figure>
    <div v-if="!guild.icon" :class="allMissingClasses">
      {{missingName}}
    </div>
    <slot name="overlay"></slot>
  </fragment>
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
    },
    classes: {
      type: String,
      default: ""
    },
    iconClasses: {
      type: String,
      default: "is-square"
    },
    missingClasses: {
      type: String,
      default: "has-background-info"
    }
  },
  computed: {
    allImageClasses() {
      return ['base-guild-icon image', this.classes, this.iconClasses].join(' ')
    },
    allMissingClasses() {
      return ['base-guild-icon missing-icon', this.classes, this.missingClasses].join(' ')
    },
    imageLink() {
      return consts.discord.guildIcon(this.guild, this.iconSize)
    },
    missingName() {
      return this.guild.name.normalize().split(/\s+/)
                 .map(w => [...w][0]).slice(0, 3).join('')
    }
  },
}
</script>

<style scoped>
.base-guild-icon {
  position: relative
}

.missing-icon {
  background-color: #BBB;
  color: #FFF;
  display: flex;
  width: 100%;
  height: 100%;
  justify-content: center;
  align-items: center;
  border-radius: 50%;
}
</style>
