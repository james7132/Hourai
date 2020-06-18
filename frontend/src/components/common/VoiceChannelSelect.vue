<template>
  <b-select placeholder="Select a channel" v-bind="$attrs" :model="value"
            @input="updateIds">
    <option v-if="allowNone"> None </option>
    <option v-for="option in options" :key="option.id"
           :native-value="option.id">
        {{option.name}}
    </option>
  </b-select>
</template>

<script>
export default {
  name: 'TextChannelSelect',
  props: {
    value: null,
    allowNone: {
      type: Boolean,
      default: true
    }
  },
  data() {
    return { ids: [] }
  },
  methods: {
    updateIds() {
      console.log(this.value)
      console.log(this.allowNone)
      console.log(this.ids)
      this.$emit('input', this.ids)
    }
  },
  computed: {
    options() {
      const selectedGuild = this.$store.getters.selectedGuild
      return !selectedGuild ? [] : selectedGuild.voice_channels
    }
  },
}
</script>
