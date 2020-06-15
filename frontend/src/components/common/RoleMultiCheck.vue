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
  props: ['value', 'guild'],
  data() {
    return { ids: [] }
  },
  methods: {
    updateIds() {
      console.log(this.options, this.ids)
      this.$emit('input', this.ids)
    }
  },
  computed: {
    options() {
      const roles = (this.guild.roles || []).reverse()
      console.log("SelectedGuild: ", this.guild, roles)
      return roles
    },
  },
}
</script>
