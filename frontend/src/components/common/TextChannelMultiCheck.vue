<template>
  <fragment>
    <div class="field" v-for="option in options" :key="option.id">
      <b-checkbox
          v-model="ids"
          :native-value="option.id"
          @input="updateIds">
          {{option.name}}
      </b-checkbox>
    </div>
  </fragment>
</template>

<script>
export default {
  name: 'RoleMultiCheck',
  props: ['value'],
  data() {
    return { ids: [] }
  },
  methods: {
    updateIds() {
      this.$emit('input', this.ids)
    }
  },
  computed: {
    options() {
      const selectedGuild = this.$store.getters.selectedGuild
      return !selectedGuild ? [] : selectedGuild.text_channels
    },
  },
}
</script>
