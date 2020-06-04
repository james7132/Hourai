<template>
  <div id="landing-page">
    <img class="logo" alt="Hourai Logo" src="@/assets/logo.webp">
    <div class="hello">
      <h1>404</h1>
      <p>Page Not Found</p>
      <p>Fumooooo</p>
      <ul>
        <li><a href="https://discord.gg/UydKWHX" target="_blank"
        rel="noopener">Discord</a></li>
        <li><a href="https://twitter.com/hourai_gg" target="_blank"
        rel="noopener">Twitter</a></li>
      </ul>
    </div>
  </div>
</template>

<script>
export default {
  name: 'LandingPage',
}
</script>

<style scoped>
.logo {
  border-radius: 50%;
}
h3 {
  margin: 40px 0 0;
}
ul {
  list-style-type: none;
  padding: 0;
}
li {
  display: inline-block;
  margin: 0 10px;
}
a {
  color: #42b983;
}
</style>
