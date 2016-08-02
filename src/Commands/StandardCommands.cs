using System;
using System.Linq;
using Discord.Commands;

namespace DrumBot {
    static class StandardCommands {

        [Command]
        [Description("Gets the avatar url of all mentioned users.")]
        [Parameter("Users", ParameterType.Multiple)]
        [Check(typeof(ProdChecker))]
        public static async void Avatar(CommandEventArgs e) {
            await Command.ForEveryUser(e, e.Message.MentionedUsers,
                async user => $"{user.Name}: {user.AvatarUrl}");
        }

        [Command]
        [Description("Removes the last X messages from the current channel.")]
        [Parameter("Message Count")]
        [Check(typeof(ProdChecker))]
        [Check(typeof(ManageMessagesChecker))]
        public static async void Prune(CommandEventArgs e) {
            int count;
            var countArg = e.GetArg("Message Count");
            if(!int.TryParse(countArg, out count)) {
                await e.Respond($"Prune failure. Cannot parse { countArg } to a valid value.");
                return;
            }
            if(count < 0) {
                await e.Respond("Cannot a negative count of messages");
                return;
            }
            if (count > Bot.Config.PruneLimit)
                count = Bot.Config.PruneLimit;
            var messages = await e.Channel.DownloadMessages(count);
            var finalCount = Math.Min(messages.Length, count);
            await e.Channel.DeleteMessages(messages.Take(count).ToArray());
            await e.Respond(Bot.Config.SuccessResponse + $" Deleted { finalCount } messages.");
        }

    }
}
