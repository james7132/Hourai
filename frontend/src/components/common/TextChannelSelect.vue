<template>
  <b-select placeholder="Select a channel" v-bind="$attrs">
    <option v-if="allowNone"> None </option>
    <option v-for="option in options" :key="option.id">
        #{{option.name}}
    </option>
  </b-select>
</template>

<script>
export default {
  name: 'TextChannelSelect',
  props: {
    allowNone: {
      type: Boolean,
      default: true
    }
  },
  computed: {
    options() {
      const selectedGuild = this.$store.getters.selectedGuild
      return !selectedGuild ? [] : selectedGuild.text_channels
    }
  },
}
</script>

<style>
</style>
