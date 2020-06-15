<template>
  <b-select placeholder="Select a role" v-bind="$attrs" :model="value">
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
  computed: {
    options() {
      const selectedGuild = this.$store.getters.selectedGuild
      return !selectedGuild ? [] : selectedGuild.roles.reversed()
    }
  },
}
</script>
