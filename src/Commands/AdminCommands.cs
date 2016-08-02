using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Administrative bot commands.
    /// </summary>
    class AdminCommands {

        [Command]
        [Description("Kicks all mentioned users. Requires ``Kick Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(KickMembersChecker))]
        public static async void Kick(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers,
                Command.AdminAction(e.Channel, "kick", user => user.Kick()));
        }

        [Command]
        [Description("Bans all mentioned users. Requires ``Ban Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(BanMembersChecker))]
        public static async void Ban(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers,
                Command.AdminAction(e.Channel, "ban", UserExtensions.Ban));
        }

        [Command]
        [Description("Server mutes all mentioned users. Requires ``Mute Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(MuteMembersChecker))]
        public static async void Mute(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers,
                Command.AdminAction(e.Channel, "mute", UserExtensions.Mute, ignoreErrors: true));
        }

        [Command]
        [Description("Server unmutes all mentioned users. Requires ``Mute Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(MuteMembersChecker))]
        public static async void Unmute(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers, 
                Command.AdminAction(e.Channel, "unmute", UserExtensions.Unmute, ignoreErrors: true));
        }

        [Command]
        [Description("Server deafens all mentioned users. Requires ``Deafen Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(DeafenMembersChecker))]
        public static async void Deafen(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers, 
                Command.AdminAction(e.Channel, "deafen", UserExtensions.Deafen, ignoreErrors: true));
        }

        [Command]
        [Description("Server undeafens all mentioned users. Requires ``Deafen Members`` permission for both user and bot")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(DeafenMembersChecker))]
        public static async void Undeafen(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers, 
                Command.AdminAction(e.Channel, "undeafen", UserExtensions.Undeafen, ignoreErrors: true));
        }
    }
}
