<template>
  <section>
    <div class="container">
      <div class="columns">
        <div class="column">
          <figure v-if="facing_right" class="image">
            <img v-bind:src="img"/>
          </figure>
          <div v-if="!facing_right" class="card">
            <header class="card-header">
              <p class="card-header-title">
                {{ title }}
              </p>
            </header>
            <div class="card-content">
              <div class="content">
                {{content}}
              </div>
            </div>
            <footer class="card-footer" v-if="footer">
              <a class="card-footer-item"
                v-for="item in footer"
                v-bind:key="item.text"
                v-bind:href="item.url">
                {{item.text}}
              </a>
            </footer>
          </div>
        </div>
        <div class="column">
          <figure
            v-if="!facing_right"
            class="image">
            <img v-bind:src="img"/>
          </figure>
          <div v-if="facing_right" class="card">
            <header class="card-header">
              <p class="card-header-title">
                {{ title }}
              </p>
            </header>
            <div class="card-content">
              <div class="content">
                {{content}}
              </div>
            </div>
            <footer class="card-footer" v-if="footer">
              <a class="card-footer-item"
                v-for="item in footer"
                v-bind:key="item.text"
                v-bind:href="item.url">
                {{item.text}}
              </a>
            </footer>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script>
export default {
  name: 'LandingPageSection',
  props: ['facing_right', 'img', 'title', 'content', 'footer'],
}
</script>

<style>
</style>
