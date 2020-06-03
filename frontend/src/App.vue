<template>
  <div id="app">
    <img class="logo" alt="Hourai Logo" src="./assets/logo.webp">
    <HelloWorld msg="Welcome to Your Vue.js App"/>
  </div>
</template>

<script>
import HelloWorld from './components/HelloWorld.vue'

export default {
  name: 'App',
  components: {
    HelloWorld
  }
}
</script>

<style>
@import url('https://fonts.googleapis.com/css2?family=Montserrat&display=swap');

#app {
  font-family: 'Montserrat', sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-align: center;
  color: #FFFFFF;
  margin-top: 60px;
}

.logo {
  border-radius: 50%;
}

html, body {
  margin: 0;
  padding: 0;
  border: 0;
  background-color: #2C2F33;
  height: 100%;
}
</style>
