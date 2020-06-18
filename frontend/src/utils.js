export default {
  titleCase(val) {
      let str = val.toLowerCase().split(' ')
      for (let i = 0; i < str.length; i++) {
         str[i] = str[i].charAt(0).toUpperCase() + str[i].slice(1)
      }
      return str.join(' ')
  },
  isPromise(obj) {
    return obj !== null && typeof obj.then === 'function'
  }
}
