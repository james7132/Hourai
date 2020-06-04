<template>
  <section class="hero is-dark is-small">
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
        <a href="https://discordapp.com/oauth2/authorize?client_id=208460637368614913&scope=bot&permissions=2147483647">
          <button class="button is-primary is-rounded">Add to Server</button>
        </a>
      </div>
    </div>
  </section>
</template>

<script>
import LandingNavBar from './NavBar.vue'

export default {
  name: 'LandingPageHero',
  components: {LandingNavBar}
}
</script>

<style scoped>
#logo {
  margin-left: auto;
  margin-right: auto;
  margin-bottom: 5%;
}
</style>
