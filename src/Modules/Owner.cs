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
  [Remarks("Gets the log for the bot.")]
  public async Task Log(IUserMessage message) {
    await message.Channel.SendFileRetry(Bot.BotLog);
  }

  [Command("uptime")]
  [Remarks("Gets the bot's uptime since startup")]
  public async Task Uptime(IUserMessage message) {
    await message.Respond($"Start Time: {Bot.StartTime}\nUptime: {Bot.Uptime}");
  }

  [Command("counters")] 
  [Remarks("Gets all of the counters and their values.")]
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
  [Remarks("Turns off the bot.")]
  public async Task Kill(IUserMessage message) {
    await message.Success();
    Environment.Exit(-1);
  }

  [Command("broadcast")]
  [Remarks("Broadcasts a message to the default channel of all servers the bot is connected to.")]
  public async Task Broadcast(IUserMessage message, [Remainder] string broadcast) {
    var guilds = await Bot.Client.GetGuildsAsync();
    var defaultChannels = await Task.WhenAll(guilds.Select(g => g.GetDefaultChannelAsync()));
    await Task.WhenAll(defaultChannels.Select(c => c.Respond(broadcast)));
  }

  [Command("save")]
  [Remarks("Forces the bot to flush and save all active information")]
  public async Task SaveAll(IUserMessage msg) {
    foreach(var config in Config.ServerConfigs)
      config.Save();
    await msg.Success();
  }

  [Command("stats")]
  [Remarks("Gets statistics about the current running bot instance")]
  public async Task Stats(IUserMessage msg) {
    var client = Bot.Client;
    var builder = new StringBuilder();
    var guilds = await client.GetGuildsAsync();
    var usersTask = Task.WhenAll(guilds.Select(g => g.GetUsersAsync()));
    var channelsTask = Task.WhenAll(guilds.Select(g => g.GetChannelsAsync()));
    var users = (await usersTask).Sum(u => u.Count);
    var channels = (await channelsTask).Sum(c => c.Count);
    builder.AppendLine("Stats".Bold());
    builder.AppendLine($"Connected Servers: {guilds.Count}");
    builder.AppendLine($"Visible Users: {users}");
    builder.AppendLine($"Visible Channels: {channels}");
    builder.AppendLine();
    builder.AppendLine($"Start Time: {Bot.StartTime})");
    builder.AppendLine($"Uptime: {Bot.Uptime}");
    builder.AppendLine();
    builder.AppendLine($"Shard ID:: {client.ShardId}");
    builder.AppendLine($"Latency:: {client.Latency}");
    builder.AppendLine($"Total Memory Used: {BytesToMemoryValue(GC.GetTotalMemory(false))}");
    await msg.Respond(builder.ToString());
  }

  static readonly string[] SizeSuffixes = {"B","KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

  static string BytesToMemoryValue(long bytes) {
    if(bytes < 0) return "-" + BytesToMemoryValue(-bytes);
    if(bytes == 0) return "0B";

    int mag = (int) Math.Log(bytes, 1024);
    decimal adjustedSize = (decimal) bytes / (1L << (mag * 10));
    return string.Format("{0:n1}{1}", adjustedSize, SizeSuffixes[mag]);
  }

}

}
