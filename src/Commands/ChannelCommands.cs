using System.Linq;
using Discord.Commands;

namespace DrumBot {
    class ChannelCommands {
        [Command]
        [Group("channel")]
        [Description("Marks all mentioned channels for ignoring search. Requires ``Manage Channels`` permission.")]
        [Parameter("Channels", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(UserManageChannelsChecker))]
        public static async void Ignore(CommandEventArgs e) {
            e.Server.GetConfig().AddIgnoredChannels(e.Message.MentionedChannels.Select(c => c.Id).ToArray());
            await e.Respond(Bot.Config.SuccessResponse);
        }

        [Command]
        [Group("channel")]
        [Description("Marks all mentioned channels to stop ignoring search. Requires ``Manage Channels`` permission.")]
        [Parameter("Channels", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        [Check(typeof(UserManageChannelsChecker))]
        public static async void Unignore(CommandEventArgs e) {
            e.Server.GetConfig().RemoveIgnoredChannels(e.Message.MentionedChannels.Select(c => c.Id).ToArray());
            await e.Respond(Bot.Config.SuccessResponse);
        }
    }
}
