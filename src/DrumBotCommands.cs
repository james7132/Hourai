using System.Threading.Tasks;
using Discord.Commands;
using DrumBot.src.Attributes;

namespace DrumBot.src {
    public class DrumBotCommands {

        [Command("serverconfig")]
        [Log]
        static async Task ServerConfigCommand(CommandEventArgs e) {
            await e.Channel.SendMessage(
                    $"{e.User.Mention}, here is the server config for {e.Server.Name}:\n{DrumBot.Config.GetServerConfig(e.Server)}");
        }

        [Command("search")]
        [Alias("find")]
        [Log]
        [CheckPermissions]
        public static async Task SearchCommand(CommandEventArgs e) {
            string reply = await DrumBot.ChannelSet.Get(e.Channel).Search(e.GetArg("SearchTerm"));
            await e.Channel.SendMessage($"{e.User.Mention}: Matches found in {e.Channel.Mention}:\n{reply}");
        }

            //commandService.CreateCommand("avatar")
            //        .Description("Gets the avatar URLs for specified members")
            //        .Do(async e => {
            //            Log.Info($"Command Triggered: Avatar by { e.User.ToIDString() }");
            //            if(!e.Message.MentionedUsers.Any()) {
            //                await e.Channel.SendMessage("No user(s) specified. Please mention at least one user.");
            //                return;
            //            }
            //            var stringBuilder = new StringBuilder();
            //            foreach (User user in e.Message.MentionedUsers) {
            //                stringBuilder.AppendLine(user.AvatarUrl);
            //            }
            //            await e.Channel.SendMessage(stringBuilder.ToString());
            //        });
    }
}
