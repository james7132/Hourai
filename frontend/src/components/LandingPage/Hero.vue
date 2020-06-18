<template>
  <section class="hero is-fullheight">
    <div class="hero-head">
      <LandingNavBar></LandingNavBar>
    </div>
    <div class="hero-body">
      <div class="container has-text-centered">
        <figure id="logo" class="image is-128x128">
          <img class="is-rounded" src="@/assets/logo.webp"/>
        </figure>
        <h1 class="title">Hourai</h1>
        <h2 class="subtitle">
          The world's most advanced security and moderation bot for Discord.
        </h2>
        <a :href="botLink">
          <button class="button is-primary is-rounded">Add to Server</button>
        </a>
      </div>
    </div>
    <nav id="stats" class="level has-text-centered">
      <div v-for="stat in stats" v-bind:key="stat.title"
          class="level-item">
        <div>
          <p class="heading">{{stat.title}}</p>
          <p class="title">{{stat.value}}</p>
        </div>
      </div>
    </nav>
  </section>
</template>

<script>
import consts from '@/consts.js'
import LandingNavBar from './NavBar.vue'

function summary_count(count) {
  return new Intl.NumberFormat({
    maximumSignificantDigits: 3,
    notation: 'compact',
    compactDisplay: 'short',
  }).format(count)
}

function sum_stats(stats, keys) {
  let sum = {}
  for (let shard in stats) {
    for (let key of keys) {
      if (sum[key] === undefined) {
        sum[key] = 0
      }
      sum[key] += stats[shard][key]
    }
  }
  return sum
}

export default {
  name: 'LandingPageHero',
  components: {LandingNavBar},
  async mounted() {
    this.schedule_refresh_stats()
    await this.refresh_stats()
  },
  data() {
    return {
      botLink: consts.links.bot,
      stats: [{
        title: "Servers",
        value: "..."
      }, {
        title: "Users",
        value: "..."
      }, {
        title: "Messages Proccesed",
        value: "..."
      }],
      intervalStats: ''
    }
  },
  beforeDestroy() {
    clearInterval(this.intervalStats)
  },
  methods: {
    schedule_refresh_stats() {
      this.intervalStats = setInterval(this.refresh_stats, 10000)
    },
    async refresh_stats() {
      let response = await this.$api.bot_status().get()
      let summary = sum_stats(response.data.shards,
                              ['guilds', 'members', 'messages'])
      this.stats = [{
          title: "Servers",
          value: summary_count(summary['guilds']),
        }, {
          title: "Users",
          value: summary_count(summary['members']),
        }, {
          title: "Messages Proccesed",
          value: summary_count(summary['messages']),
        }]
    }
  }
}
</script>

<style scoped>
#logo {
  margin-left: auto;
  margin-right: auto;
  margin-bottom: 5%;
}

#stats {
  margin-left: 20%;
  margin-right: 20%;
  margin-bottom: 3%;
}

.vertically-centered {
  display: flex;
  align-items: center;
}
</style>
