<template>
  <div class="content">
    <figure class="image is-128x128">
      <img class="is-rounded"
        src="https://www.rightpointe.com/wp-content/uploads/2018/02/hardhat.svg">
      <slot #overlay></slot>
    </figure>
    <h1>Under Construction.</h1>
    <h2>Coming soon!</h2>
  </div>
</template>

<script>
export default {
  name: 'UnderConstruction',
}
</script>

<style scoped>
.content {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  height: 100%;
  width: 100%;
}
</style>
