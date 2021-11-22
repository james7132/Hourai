<template>
  <div>
    <b-loading v-model:active="isLoading"
      :can-cancel="false"></b-loading>
    <div v-if="!isLoading">
      <span>{{updated.voice_channel_id}}</span>
      <b-field>
        <template v-slot:label>
          <b-tooltip type="is-primary"
          label="If set, the bot will only play music in this channel.">
            Music Voice Channel
          </b-tooltip>
        </template>
        <VoiceChannelSelect expanded rounded :model="updated.voice_channel_id">
        </VoiceChannelSelect>
      </b-field>
      <span>{{updated}}</span>
      <div class="columns">
        <div class="column">
          <span>{{updated.dj_role_id}}</span>
          <b-field>
            <template v-slot:label>
              <b-tooltip type="is-primary"
              label="Users with these roles will be able to use DJ level music commands.">
                DJ Roles
              </b-tooltip>
            </template>
            <RoleMultiCheck
              v-model="updated.dj_role_id"
              :guild="selectedGuild">
            </RoleMultiCheck>
          </b-field>
        </div>
        <div class="column">
          <span>{{updated.text_channel_id}}</span>
          <b-field>
            <template v-slot:label>
              <b-tooltip type="is-primary"
              label="If any are set, music commands can only be used in these channels">
                Text Channels
              </b-tooltip>
            </template>
            <TextChannelMultiCheck v-model="updated.text_channel_id">
            </TextChannelMultiCheck >
          </b-field>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import RoleMultiCheck from '@/components/common/RoleMultiCheck.vue'
import TextChannelMultiCheck  from '@/components/common/TextChannelMultiCheck.vue'
import VoiceChannelSelect from '@/components/common/VoiceChannelSelect.vue'
import {mapGetters} from "vuex"

export default {
  name: 'GuildConfigMusic',
  components: {
    RoleMultiCheck,
    TextChannelMultiCheck ,
    VoiceChannelSelect,
  },
  computed: mapGetters([
    "selectedGuild"
  ]),
  data() {
    const baseTemplate = {
      dj_role_id: [],
      voice_channel_id: null,
      text_channel_id: []
    }
    const templateJson = JSON.stringify(baseTemplate)
    return {
      current: JSON.parse(templateJson),
      updated: JSON.parse(templateJson),
      isLoading: false
    }
  },
}
</script>

<style>
</style>
