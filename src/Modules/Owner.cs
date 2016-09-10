using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module(AutoLoad = false)]
    [Hide]
    [BotOwner]
    public class Owner {

        readonly CounterSet Counters;

        public Owner(CounterSet counters) { Counters = counters; }

        [Command("log")]
        [Description("Gets the log for the bot.")]
        public async Task Log(IUserMessage message) {
            await message.Channel.SendFileRetry(Bot.BotLog);
        }

        [Command("uptime")]
        [Description("Gets the bot's uptime since startup")]
        public async Task Uptime(IUserMessage message) {
            await message.Respond($"Start Time: {Bot.StartTime}\nUptime: {Bot.Uptime}");
        }

        [Command("counters")] 
        [Description("Gets all of the counters and their values.")]
        public async Task Counter(IUserMessage message) {
            var response = new StringBuilder();
            var results = from counter in Counters
                          orderby (counter.Value as IReadableCounter)?.Value descending
                          select new { name = counter.Key, value = counter.Value };
            foreach (var counter in results) {
                var readable = counter.value as IReadableCounter;
                if (readable == null)
                    continue;
                response.AppendLine($"{counter.name}: {readable.Value}");
            }
            await message.Respond(response.ToString());
        }

        [Command("kill")]
        [Description("Turns off the bot.")]
        public async Task Kill(IUserMessage message) {
            await message.Success();
            Environment.Exit(-1);
        }

        [Command("broadcast")]
        [Description("Broadcasts a message to the default channel of all servers the bot is connected to.")]
        public async Task Broadcast(IUserMessage message, [Remainder] string broadcast) {
            var guilds = await Bot.Client.GetGuildsAsync();
            var defaultChannels = await Task.WhenAll(guilds.Select(g => g.GetDefaultChannelAsync()));
            await Task.WhenAll(defaultChannels.Select(c => c.Respond(broadcast)));
        }

    }
}
