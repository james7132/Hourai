<template>
<aside id="sidebar-menu" class="menu">
  <p class="menu-label">General</p>
  <ul class="menu-list">
    <li><a>Overview</a></li>
  </ul>
  <ul class="menu-list">
    <li><a>Analytics</a></li>
  </ul>
  <p class="menu-label">Configuration</p>
  <ul class="menu-list">
    <li v-for="config in configs" :key="config.label">
      <a>{{config.label}}</a>
    </li>
  </ul>
</aside>
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

<style scoped>
#sidebar-menu {
  padding-top: 15%;
}
</style>
