<template>
  <b-menu>
     <b-menu-list
        v-for="category in categories"
        :key="category.name"
        :label="category.name">
       <b-menu-item
        v-for="entry in category.entries"
        :key="entry.name"
        :label="titleCase(entry.name)"
        tag="router-link"
        :active="entry.name == $route.name"
        :to="makeRoute(entry.name)">
       </b-menu-item>
     </b-menu-list>
  </b-menu>
</template>

<script>
import utils from '@/utils.js'
import GuildViews from './configs/router.js'

export default {
  name: 'DashboardMenu',
  data() {
    return { categories: GuildViews.categories }
  },
  methods: {
    titleCase: utils.titleCase,
    makeRoute(name) {
      return {
        name: name,
        params: {
          guild_id: this.$route.params.guild_id
        }
      }
    }
  }
}
</script>
