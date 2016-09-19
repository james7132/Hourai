using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Discord.Commands;

namespace Hourai {

[Module(AutoLoad = false)]
[Hide]
[BotOwner]
public class Owner {

  readonly CounterSet Counters;

  public Owner(CounterSet counters) { Counters = counters; }

  [Command("log")]
  [Remarks("Gets the log for the bot.")]
  public async Task GetLog(IUserMessage message) {
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
    await Bot.Database.Save();
    await message.Success();
    Bot.Exit();
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
    await Bot.Database.Save();
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
      .AppendLine($"Stored Users {Bot.Database.Users.Count()}")
      .AppendLine($"Visible Channels: {channels}")
      .AppendLine($"Stored Users {Bot.Database.Channels.Count()}")
      .AppendLine()
      .AppendLine($"Start Time: {Bot.StartTime})")
      .AppendLine($"Uptime: {Bot.Uptime}")
      .AppendLine()
      .AppendLine($"Client: Discord .NET v{DiscordConfig.Version} (API v{DiscordConfig.APIVersion}, {DiscordSocketConfig.GatewayEncoding})")
      .AppendLine($"Shard ID: {client.ShardId}")
      .AppendLine($"Total Shards: {config.TotalShards}")
      .AppendLine($"Latency: {client.Latency}ms");
    using(Process proc = Process.GetCurrentProcess()) {
      proc.Refresh();
      builder.AppendLine($"Total Memory Used: {BytesToMemoryValue(proc.PrivateMemorySize64)}");
    }
    await msg.Respond(builder.ToString());
  }

  [Command("refresh")]
  public async Task Refresh(IUserMessage msg) {
    var client = Bot.Client;
    var guilds = await client.GetGuildsAsync();
    var db = Bot.Database;
    Log.Info("Starting refresh...");
    foreach(var guild in guilds) {
      db.AllowSave = false;
      var guildDb = await Bot.Database.GetGuild(guild);
      Log.Info($"Refreshing {guild.Name}...");
      var cTask = guild.GetChannelsAsync();
      var uTask = guild.GetUsersAsync();
      await Task.WhenAll(cTask, uTask);
      var channels = cTask.Result.OfType<ITextChannel>();
      var users = uTask.Result;
      foreach(var channel in channels)
        await db.GetChannel(channel);
      foreach(var user in users)
        await db.GetGuildUser(user);
      Bot.Database.AllowSave = true;
      await Bot.Database.Save();
    }
    Log.Info("Done refreshing.");
    await msg.Success();
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

  [Command("reavatar")]
  [Remarks("Changes the avatar of the bot.")]
  public async Task Reavatar(IUserMessage msg, [Remainder] string url = "") {
    if(msg.Attachments.Any())
      url = msg.Attachments.First().Url;
    if(msg.Embeds.Any())
      url = msg.Embeds.First().Url;
    if(string.IsNullOrEmpty(url)) {
      await msg.Respond("No provided image.");
      return;
    }
    Log.Info(url);
    using(var client = new HttpClient())
    using(var contentStream = await client.GetStreamAsync(url)) {
      await Bot.User.ModifyAsync(u => {
          u.Avatar = contentStream;
        });
    }
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
    var config = await Bot.Database.GetGuild(guild);
    config.IsBlacklisted = true;
    await msg.Success();
    await guild.LeaveAsync();
    await Bot.Database.Save();
  }

}

}

