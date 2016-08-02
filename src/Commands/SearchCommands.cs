using System.IO;
using System.Text;
using Discord;
using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Log searching relate commands
    /// </summary>
    static class SearchCommands {
        [Command]
        [Description("Search the history of the current channel for a certain value.")]
        [Parameter("SearchTerm")]
        [Check(typeof(ProdChecker))]
        public static async void Search(CommandEventArgs e) {
            string reply = await Bot.ChannelSet.Get(e.Channel).Search(e.GetArg("SearchTerm"));
            await e.Respond($"Matches found in {e.Channel.Name}:\n{reply}");
        }

        [Command]
        [Group("search")]
        [Description("Search the history of the all channels for a certain value.")]
        [Parameter("SearchTerm")]
        [Check(typeof(ProdChecker))]
        public static async void All(CommandEventArgs e) {
            var builder = new StringBuilder();
            var serverConfig = e.Server.GetConfig();
            foreach (Channel channel in e.Server.TextChannels) {
                if (e.Channel != channel && serverConfig.IsIgnored(channel))
                    continue;
                string result = await Bot.ChannelSet.Get(channel).Search(e.GetArg("SearchTerm"));
                if (string.IsNullOrEmpty(result))
                    continue;
                builder.AppendLine($"Matches found in {channel.Name}:");
                builder.AppendLine(result);
            }
            await e.Respond(builder.ToString());
        }

        [Command]
        [Description("Get the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
        [Parameter("Day")]
        [Check(typeof(ProdChecker))]
        public static async void GetLog(CommandEventArgs e) {
            string path = Bot.ChannelSet.Get(e.Channel).GetPath(e.GetArg("Day"));
            if (File.Exists(path))
                await e.Channel.SendFile(path);
            else
                await e.Channel.SendMessage($"A log for { e.Channel.Mention } on date { e.GetArg("Day") } cannot be found.");
        }
    }
}
