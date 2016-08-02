using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Channel ignoring related commands.
    /// </summary>
    class ChannelCommands {
        [Command]
        [Group("channel")]
        [Description("Marks all mentioned channels for ignoring search. Requires ``Manage Channels`` permission.")]
        [Parameter("Channels", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(UserManageChannelsChecker))]
        public static async void Ignore(CommandEventArgs e) {
            await e.Server.GetConfig().AddIgnoredChannels(e.Message.MentionedChannels);
            await e.Respond(Bot.Config.SuccessResponse);
        }

        [Command]
        [Group("channel")]
        [Description("Marks all mentioned channels to stop ignoring search. Requires ``Manage Channels`` permission.")]
        [Parameter("Channels", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(UserManageChannelsChecker))]
        public static async void Unignore(CommandEventArgs e) {
            await e.Server.GetConfig().RemoveIgnoredChannels(e.Message.MentionedChannels);
            await e.Respond(Bot.Config.SuccessResponse);
        }
    }
}
