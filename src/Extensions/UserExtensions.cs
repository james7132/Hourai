using System.Threading.Tasks;
using Discord;

namespace DrumBot {
    public static class UserExtensions {

        /// <summary> Self-explanatory helper functions to alter the state of a user</summary>
        public static Task SetMuted(this User user, bool value) => user.Edit(isMuted: value);
        public static Task SetDeafen(this User user, bool value) => user.Edit(isDeafened: value);
        public static Task Mute(this User user) => user.SetMuted(true);
        public static Task Deafen(this User user) => user.SetDeafen(true);
        public static Task Unmute(this User user) => user.SetMuted(false);
        public static Task Undeafen(this User user) => user.SetDeafen(false);

        public static bool IsBotOwner(this User user) => user.Id == Bot.Config.Owner;
        public static bool IsServerOwner(this User user) => user.Server.Owner == user;

        public static Task Ban(this User user) => user.Server.Ban(user);
    }
}
