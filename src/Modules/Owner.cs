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

[Hide]
[BotOwner]
[DontAutoLoad]
public class Owner : HouraiModule {

  readonly CounterSet Counters;

  public Owner(CounterSet counters) { Counters = counters; }

  [Command("log")]
  [Remarks("Gets the log for the bot.")]
  public async Task GetLog() {
    await Context.Channel.SendFileRetry(Bot.Get<LogService>().BotLog);
  }

  [Command("counters")] 
  [Remarks("Gets all of the counters and their values.")]
  public async Task Counter() {
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
    await RespondAsync(response.ToString());
  }

  [Command("kill")]
  [Remarks("Turns off the bot.")]
  public async Task Kill() {
    await Bot.Database.Save();
    await Success();
    Bot.Exit();
  }

  [Command("broadcast")]
  [Remarks("Broadcasts a message to the default channel of all servers the bot is connected to.")]
  public async Task Broadcast([Remainder] string broadcast) {
    var guilds = Bot.Client.Guilds;
    var defaultChannels = await Task.WhenAll(guilds.Select(g => g.GetDefaultChannelAsync()));
    await Task.WhenAll(defaultChannels.Select(c => c.Respond(broadcast)));
  }

  [Command("save")]
  [Remarks("Forces the bot to flush and save all active information")]
  public async Task SaveAll() {
    await Bot.Database.Save();
    await Success();
  }

  [Command("stats")]
  [Remarks("Gets statistics about the current running bot instance")]
  public async Task Stats() {
    var client = Bot.Client;
    var config = Bot.ClientConfig;
    var builder = new StringBuilder();
    var guilds = client.Guilds;
    builder.AppendLine("Stats".Bold())
      .AppendLine($"Connected Servers: {guilds.Count}")
      .AppendLine($"Visible Users: {client.Guilds.Sum(g => g.Users.Count)}")
      .AppendLine($"Stored Users {Bot.Database.Users.Count()}")
      .AppendLine($"Visible Channels: {client.Guilds.Sum(g => g.Channels.Count)}")
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
    await Context.Message.Respond(builder.ToString());
  }

  [Command("refresh")]
  public async Task Refresh() {
    var client = Bot.Client;
    var db = Bot.Database;
    Log.Info("Starting refresh...");
    foreach(var guild in client.Guilds) {
      db.AllowSave = false;
      var guildDb = await Bot.Database.GetGuild(guild);
      Log.Info($"Refreshing {guild.Name}...");
      var channels = await guild.GetTextChannelsAsync();
      foreach(var channel in channels)
        await db.GetChannel(channel);
      foreach(var user in guild.Users)
        await db.GetGuildUser(user);
      Bot.Database.AllowSave = true;
      await Bot.Database.Save();
    }
    Log.Info("Done refreshing.");
    await Success();
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
  public async Task Rename(string name) {
    await Bot.User.ModifyAsync(u => {
          u.Username = name;
        });
    await Success();
  }

  [Command("reavatar")]
  [Remarks("Changes the avatar of the bot.")]
  public async Task Reavatar([Remainder] string url = "") {
    if(Context.Message.Attachments.Any())
      url = Context.Message.Attachments.First().Url;
    if(Context.Message.Embeds.Any())
      url = Context.Message.Embeds.First().Url;
    if(string.IsNullOrEmpty(url)) {
      await Context.Message.Respond("No provided image.");
      return;
    }
    Log.Info(url);
    using(var client = new HttpClient())
    using(var contentStream = await client.GetStreamAsync(url)) {
      await Bot.User.ModifyAsync(u => {
          u.Avatar = new Discord.API.Image(contentStream);
        });
    }
    await Success();
  }

  [Command("leave")]
  [Remarks("Makes the bot leave the current server")]
  public async Task Leave() {
    var guild = Check.InGuild(Context.Message).Guild;
    await Success();
    await guild.LeaveAsync();
  }

  [Group("blacklist")]
  public class BlacklistGroup : HouraiModule {

    static bool SettingToBlacklist(string setting) {
      if(setting == "-")
        return false;
      return true;
    }

    [Command("server")]
    [Remarks("Blacklists the current server and makes the bot leave.")]
    public async Task Server(string setting = "+") {
      var guild = Check.InGuild(Context.Message).Guild;
      var config = await Bot.Database.GetGuild(guild);
      config.IsBlacklisted = true;
      await Bot.Database.Save();
      await Success();
      await guild.LeaveAsync();
    }

    [Command("user")]
    [Remarks("Blacklist user(s) and prevent them from using commands.")]
    public async Task User(string setting, params IGuildUser[] users) {
      var blacklisted = SettingToBlacklist(setting);
      foreach(var user in users) {
        var uConfig = await Bot.Database.GetUser(user);
        if(uConfig == null)
          continue;
        uConfig.IsBlacklisted = blacklisted;
        if(!blacklisted)
          continue;
        var dmChannel = await user.CreateDMChannelAsync();
        await dmChannel.Respond("You have been blacklisted. The bot will no longer respond to your commands.");
      }
      await Bot.Database.Save();
      await Success();
    }

  }

}

}

