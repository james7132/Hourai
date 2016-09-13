using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
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
    var config = Bot.ClientConfig;
    var builder = new StringBuilder();
    var guilds = await client.GetGuildsAsync();
    var usersTask = Task.WhenAll(guilds.Select(g => g.GetUsersAsync()));
    var channelsTask = Task.WhenAll(guilds.Select(g => g.GetChannelsAsync()));
    var users = (await usersTask).Sum(u => u.Count);
    var channels = (await channelsTask).Sum(c => c.Count);
    builder.AppendLine("Stats".Bold())
      .AppendLine($"Connected Servers: {guilds.Count}")
      .AppendLine($"Visible Users: {users}")
      .AppendLine($"Visible Channels: {channels}")
      .AppendLine()
      .AppendLine($"Start Time: {Bot.StartTime})")
      .AppendLine($"Uptime: {Bot.Uptime}")
      .AppendLine()
      .AppendLine($"Client: Discord .NET v{DiscordConfig.Version} (API v{DiscordConfig.APIVersion}, {DiscordSocketConfig.GatewayEncoding})")
      .AppendLine($"Shard ID: {client.ShardId}")
      .AppendLine($"Total Shards: {config.TotalShards}")
      .AppendLine($"Latency: {client.Latency}ms")
      .AppendLine($"Total Memory Used: {BytesToMemoryValue(GC.GetTotalMemory(false))}");
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

  [Command("rename")]
  [Remarks("Renames the bot a new name.")]
  public async Task Rename(IUserMessage msg, string name) {
    await Bot.User.ModifyAsync(u => {
          u.Username = name;
        });
    await msg.Success();
  }

  [Command("leave")]
  [Remarks("Makes the bot leave the current server")]
  public async Task Leave(IUserMessage msg) {
    var guild = Check.InGuild(msg).Guild;
    await msg.Success();
    await guild.LeaveAsync();
  }

  [Command("blacklist")]
  [Remarks("Blacklists the current server and makes teh bot leave.")]
  public async Task Blacklist(IUserMessage msg) {
    var guild = Check.InGuild(msg).Guild;
    var config = Config.GetGuildConfig(guild);
    config.IsBlacklisted = true;
    config.Save();
    await msg.Success();
    await guild.LeaveAsync();
  }

}

}
