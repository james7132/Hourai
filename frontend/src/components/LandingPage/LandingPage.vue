<template>
  <fragment>
    <LandingHero></LandingHero>
  </fragment>
</template>

<script>
import LandingHero from './Hero.vue'
/*import LandingSection from './Section.vue'*/
/*import LandingFooter from './Footer.vue'*/

export default {
  name: 'LandingPage',
  components: {LandingHero},
  data: function() {
    return {
      sections: [{
        title: 'Verification',
        facing_right: true,
        img: 'https://bulma.io/images/placeholders/1280x960.png',
        content: `Vet users as they join your server and isolate potential
        troublemakers before they have a chance to wreck havoc.`,
        footer: [{text: 'Test'}]
      }, {
        title: 'Escalation',
        facing_right: false,
        img: 'https://bulma.io/images/placeholders/1280x960.png',
        content: `Human memory can be flawed and biased. Improve user-moderator
        confidence by establishing a fair and progressive escalation ladder.
        Fully configurable and auditable for moderator accountability.`,
        footer: []
      }, {
        title: 'Automoderation',
        facing_right: true,
        img: 'https://bulma.io/images/placeholders/1280x960.png',
        content: `Big server and too few mods? Lessen the load on your mods
        by letting Hourai handle bad
        Fully configurable with tight integration with the escalation ladder.`,
        footer: []
      }, {
        title: 'Customization',
        facing_right: false,
        img: 'https://bulma.io/images/placeholders/1280x960.png',
        footer: []
      }]
    }
  }
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
