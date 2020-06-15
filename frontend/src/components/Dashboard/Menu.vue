<template>
  <b-menu>
     <b-menu-list label="General">
       <b-menu-item label="Overview" tag="router-link" target="_blank" to="/">
       </b-menu-item>
       <b-menu-item label="Analytics" tag="router-link" target="_blank" to="/">
       </b-menu-item>
     </b-menu-list>
     <b-menu-list label="Configuration">
       <b-menu-item v-for="config in configs" :key="config.label"
         :label="config.label" tag="router-link" target="_blank" to="/">
       </b-menu-item>
     </b-menu-list>
  </b-menu>
</template>

<script>
export default {
  name: 'DashboardMenu',
  data() {
    return {
      configs: [{
        label: "Logging"
      }, {
        label: "Moderation"
      }, {
        label: "Music"
      }, {
        label: "Roles"
      }]
    }
  }
}
</script>
