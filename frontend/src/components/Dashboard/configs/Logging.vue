<template>
  <div>
    <b-field>
      <template slot="label">
        <b-tooltip type="is-primary"
        label="If set, the bot will write activity logs to this channel.">
          Modlog Channel
        </b-tooltip>
      </template>
      <TextChannelSelect expanded rounded :model="updated.modlog_channel_id">
      </TextChannelSelect>
    </b-field>
    <b-field label="Log Deleted Messages">
      <b-switch v-model="updated.log_deleted_messages"
        :disabled="!updated.modlog_channel_id">
      </b-switch>
    </b-field>
    <b-field label="Log Edited Messages">
      <b-switch v-model="updated.log_edited_messages" disabled>
      </b-switch>
    </b-field>
    {{updated}}
  </div>
</template>

<script>
import TextChannelSelect from '@/components/common/TextChannelSelect.vue'
import {mapGetters} from "vuex"

export default {
  name: 'GuildConfigLogging',
  components: {
    TextChannelSelect,
  },
  computed: mapGetters([
    "selectedGuild"
  ]),
  data() {
    const baseTemplate = {
      modlog_channel_id: null,
      log_deleted_messages: false,
      log_edited_messages: false,
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
