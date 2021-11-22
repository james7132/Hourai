<template>
  <div>
    <b-field label="Enabled">
      <b-switch v-model="updated.enabled">
      </b-switch>
    </b-field>
    <b-field>
      <template v-slot:label>
        <b-tooltip type="is-primary"
        label="The role to give users upon verification.">
          Verified Role
        </b-tooltip>
      </template>
      <RoleSelect expanded rounded :model="updated.role_id">
      </RoleSelect>
    </b-field>
    <b-field label="Reject Default Avatars">
      <b-switch v-model="updated.avatar.reject_default_avatars">
      </b-switch>
    </b-field>
    <b-field label="Reject Banned Users">
      <b-switch v-model="updated.cross_server.reject_banned_users">
      </b-switch>
    </b-field>
    <b-field label="Minimum Server Size"
             v-if="updated.cross_server.reject_banned_users">
      <b-switch v-model="updated.cross_server.minimum_guild_size">
      </b-switch>
    </b-field>
    {{updated}}
  </div>
</template>

<script>
import RoleSelect from '@/components/common/RoleSelect.vue'
import {mapGetters} from "vuex"

export default {
  name: 'GuildConfigValidation',
  components: {
    RoleSelect,
  },
  computed: mapGetters([
    "selectedGuild"
  ]),
  data() {
    const baseTemplate = {
      enabled: false,
      role_id: null,
      avatar: {
        reject_default_avatars: true,
      },
      cross_server: {
        reject_banned_users: true,
        minimum_guild_size: 150,
      }
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
