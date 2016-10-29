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

[BotOwner]
public class Owner : DatabaseHouraiModule {

  Bot Bot { get; }
  DiscordSocketClient Client { get; }
  CounterSet Counters { get; }
  LogService LogService { get; }

  public Owner(Bot bot,
               CounterSet counters, 
               BotDbContext db,
               LogService logs,
               DiscordSocketClient client) : base(db) { 
    Bot = bot;
    Counters = counters; 
    LogService = logs;
    Client = client;
  }

  [Command("log")]
  [Remarks("Gets the log for the bot.")]
  public async Task GetLog() {
    await Context.Channel.SendFileRetry(LogService.BotLog);
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
    await Database.Save();
    await Success();
    Bot.Exit();
  }

  [Command("broadcast")]
  [Remarks("Broadcasts a message to the default channel of all servers the bot is connected to.")]
  public async Task Broadcast([Remainder] string broadcast) {
    var guilds = Client.Guilds;
    var defaultChannels = await Task.WhenAll(guilds.Select(g => g.GetDefaultChannelAsync()));
    await Task.WhenAll(defaultChannels.Select(c => c.Respond(broadcast)));
  }

  [Command("save")]
  [Remarks("Forces the bot to flush and save all active information")]
  public async Task SaveAll() {
    await Database.Save();
    await Success();
  }

  [Command("stats")]
  [Remarks("Gets statistics about the current running bot instance")]
  public async Task Stats() {
    var builder = new StringBuilder();
    var guilds = Client.Guilds;
    builder.AppendLine("Stats".Bold())
      .AppendLine($"Guilds: Visible: {guilds.Count}, Stored: {Database.Guilds.Count()}")
      .AppendLine($"Users: Visible: {Client.Guilds.Sum(g => g.Users.Count)}, Stored: {Database.Users.Count()}")
      .AppendLine($"Channels: Visible: {Client.Guilds.Sum(g => g.Channels.Count)}, Stored: {Database.Channels.Count()}")
      .AppendLine()
      .AppendLine($"Start Time: {Bot.StartTime}")
      .AppendLine($"Uptime: {Bot.Uptime}")
      .AppendLine()
      .AppendLine($"Client: Discord .NET v{DiscordConfig.Version} (API v{DiscordConfig.APIVersion}, {DiscordSocketConfig.GatewayEncoding})")
      .AppendLine($"Latency: {Client.Latency}ms")
      .AppendLine($"Total Memory Used: {BytesToMemoryValue(GC.GetTotalMemory(false))}");
    await Context.Message.Respond(builder.ToString());
  }

  [Command("refresh")]
  public async Task Refresh() {
    Log.Info("Starting refresh...");
    foreach(var guild in Client.Guilds) {
      Database.AllowSave = false;
      var guildDb = Database.GetGuild(guild);
      Log.Info($"Refreshing {guild.Name}...");
      var channels = await guild.GetTextChannelsAsync();
      foreach(var channel in channels)
        Database.GetChannel(channel);
      foreach(var user in guild.Users)
        Database.GetGuildUser(user);
      Database.AllowSave = true;
      await Database.Save();
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
    var guild = Check.NotNull(Context.Guild);
    await Success();
    await guild.LeaveAsync();
  }

  [Group("blacklist")]
  public class Blacklist : DatabaseHouraiModule {

    public Blacklist(BotDbContext db) : base(db) {
    }

    static bool SettingToBlacklist(string setting) {
      if(setting == "-")
        return false;
      return true;
    }

    [Command("server")]
    [Remarks("Blacklists the current server and makes the bot leave.")]
    public async Task Server(string setting = "+") {
      var guild = Check.NotNull(Context.Guild);
      var config = Database.GetGuild(guild);
      config.IsBlacklisted = true;
      await Database.Save();
      await Success();
      await guild.LeaveAsync();
    }

    [Command("user")]
    [Remarks("Blacklist user(s) and prevent them from using commands.")]
    public async Task User(string setting, params IGuildUser[] users) {
      var blacklisted = SettingToBlacklist(setting);
      foreach(var user in users) {
        var uConfig = Database.GetUser(user);
        if(uConfig == null)
          continue;
        uConfig.IsBlacklisted = blacklisted;
        if(!blacklisted)
          continue;
        var dmChannel = await user.CreateDMChannelAsync();
        await dmChannel.Respond("You have been blacklisted. The bot will no longer respond to your commands.");
      }
      await Database.Save();
      await Success();
    }

  }

}

}

