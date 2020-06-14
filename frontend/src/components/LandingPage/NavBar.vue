<template>
  <div class="container">
    <b-navbar>
      <template slot="start">
        <NavbarSocial></NavbarSocial>
      </template>
      <template slot="end">
        <b-navbar-item
         v-for="link in links"
         v-bind:key="link.id"
         v-bind:href="link.url">
          {{link.text}}
        </b-navbar-item>
        <b-navbar-item tag="router-link" to="/dash">
          Dashboard
        </b-navbar-item>
      </template>
    </b-navbar>
  </div>
</template>

<script>
import consts from '@/consts.js'
import NavbarSocial from '@/components/common/NavbarSocial.vue'

export default {
  name: 'LandingPageNavBar',
  components: {NavbarSocial},
  data: function() {
    return {
      links: [{
        id: "docs",
        url: consts.links.docs,
        text: "Documentation"
      }, {
        id: "status",
        url: consts.links.status,
        text: "Status"
      }]
    }
  }
}
</script>

<style>
</style>
