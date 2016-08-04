using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot.src.Services {
    public class PruneService : IService {

        public Config Config { get; }

        public PruneService(Config config) {
            Config = Check.NotNull(config);
        }

        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();

            commandService.CreateCommand("prune")
                .Description("Removes the last X messages from the current channel.")
                .Parameter("Message Count")
                .AddCheck(new ProdChecker())
                .AddCheck(Check.ManageMessages())
                .Do(Prune);

            commandService.CreateGroup("prune", cbg => {
                cbg.CreateCommand("user")
                    .Description("Removes all messages from all mentioned users in the last 100 messages.")
                    .AddCheck(new ProdChecker())
                    .AddCheck(Check.ManageMessages())
                    .Do(async e => await PruneMessages(e.Channel, 100, m => e.Message.MentionedUsers.Contains(m.User)));
            });
        }

        static async Task PruneMessages(Channel channel, int count, Func<Message, bool> pred) { 
            if (count > Config.PruneLimit)
                count = Config.PruneLimit;
            var messages = await channel.DownloadMessages(count);
            var finalCount = Math.Min(messages.Length, count);
            await channel.DeleteMessages(messages.Where(pred).OrderByDescending(m => m.Timestamp).Take(count).ToArray());
            await channel.Respond(Config.SuccessResponse + $" Deleted { finalCount } messages.");
        }

        async Task Prune(CommandEventArgs e) {
            int count;
            var countArg = e.GetArg("Message Count");
            if (!int.TryParse(countArg, out count)) {
                await e.Respond($"Prune failure. Cannot parse {countArg} to a valid value.");
                return;
            }
            if (count < 0) {
                await e.Respond("Cannot a negative count of messages");
                return;
            }
            await PruneMessages(e.Channel, count, m => true);
        }
    }
}
