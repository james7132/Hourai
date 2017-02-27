using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket; using Discord.Commands; using Hourai.Model;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Hourai {

[RequireOwner]
public partial class Owner : HouraiModule {

  public Bot Bot { get; set; }
  public CounterSet Counters { get; set; }
  public LogService LogService { get; set; }

  [Command("log")]
  [Remarks("Gets the log for the bot.")]
  public Task GetLog() =>
    Context.Channel.SendFileRetry(LogService.BotLog);

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

  [Command("join")]
  [Remarks("Provides invite link to a given server by it's id")]
  public async Task Join(ulong id) {
    var guild = Context.Client.GetGuild(id);
    if (guild == null) {
      await RespondAsync("No server found");
      return;
    }
    foreach (var channel in guild.Channels) {
      var invites = await channel.GetInvitesAsync();
      var invite = invites.FirstOrDefault(i => !i.IsRevoked)?.Url;
      if (!string.IsNullOrEmpty(invite)) {
        await RespondAsync(invite);
        return;
      }
    }
    await RespondAsync("No valid invite found");
  }

  [Command("kill")]
  [Remarks("Turns off the bot.")]
  public async Task Kill() {
    await Db.Save();
    await Success();
    Bot.Exit();
  }

  [Command("broadcast")]
  [Remarks("Broadcasts a message to the default channel of all servers the bot is connected to.")]
  public async Task Broadcast([Remainder] string broadcast) {
    var guilds = Context.Client.Guilds;
    var defaultChannels = guilds.Select(g => g.GetChannel(g.Id)).Cast<ITextChannel>();
    await Task.WhenAll(defaultChannels.Select(c => c.SendMessageAsync(broadcast)));
  }

  [Command("stats")]
  [Remarks("Gets statistics about the current running bot instance")]
  public async Task Stats() {
    var builder = new StringBuilder();
    var users = (from guild in Client.Guilds
                from user in guild.Users
                select user.Id).Distinct().Count();
    builder.AppendLine("Stats".Bold())
      .AppendLine($"Guilds: Visible: {Client.Guilds.Count}, Stored: {Db.Guilds.Count()}")
      .AppendLine($"Users: Visible: {users}, Stored: {Db.Users.Count()}")
      .AppendLine($"Guild Users: Visible: {Client.Guilds.Sum(g => g.Users.Count)}, Stored: {Db.GuildUsers.Count()}")
      .AppendLine($"Channels: Visible: {Client.Guilds.Sum(g => g.Channels.Count)}, Stored: {Db.Channels.Count()}")
      .AppendLine()
      .AppendLine($"Start Time: {Bot.StartTime}")
      .AppendLine($"Uptime: {Bot.Uptime}")
      .AppendLine()
      .AppendLine($"Client: Discord .NET v{DiscordConfig.Version} (API v{DiscordConfig.APIVersion}, {DiscordSocketConfig.GatewayEncoding})")
      .AppendLine($"Latency: {Context.Client.Latency}ms")
      .AppendLine($"Total Memory Used: {BytesToMemoryValue(Process.GetCurrentProcess().WorkingSet64)}");
    await Context.Message.Respond(builder.ToString());
  }

  [Command("refresh", RunMode=RunMode.Mixed)]
  public async Task Refresh() {
    Log.Info("Starting refresh...");
    foreach(var guild in Context.Client.Guilds) {
      Db.AllowSave = false;
      var guildDb = Db.GetGuild(guild);
      Log.Info($"Refreshing {guild.Name}...");
      var channels = guild.Channels.OfType<ITextChannel>();
      try {
        foreach(var channel in channels)
          Db.GetChannel(channel);
        foreach(var user in guild.Users) {
          if(user.Username == null) {
            Log.Error($"Found user {user.Id} without a username");
            continue;
          }
          Db.GetGuildUser(user);
        }
      } finally {
        Db.AllowSave = true;
        await Db.Save();
      }
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
    using (var httpClient = new HttpClient())
    {
        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url)))
        {
            using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync())
            {
                await Bot.User.ModifyAsync(u => {
                    u.Avatar = new Optional<Image?>(new Discord.Image(contentStream));
                  });
            }
        }
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
  public class Blacklist : HouraiModule {

    static bool SettingToBlacklist(string setting) {
      if(setting == "-")
        return false;
      return true;
    }

    [Command("server")]
    [Remarks("Blacklists the current server and makes the bot leave.")]
    public async Task Server(string setting = "+") {
      var guild = Check.NotNull(Context.Guild);
      var config = Db.GetGuild(guild);
      config.IsBlacklisted = true;
      await Db.Save();
      await Success();
      await guild.LeaveAsync();
    }

    [Command("user")]
    [Remarks("Blacklist user(s) and prevent them from using commands.")]
    public async Task User(string setting, params IGuildUser[] users) {
      var blacklisted = SettingToBlacklist(setting);
      foreach(var user in users) {
        var uConfig = Db.GetUser(user);
        if(uConfig == null)
          continue;
        uConfig.IsBlacklisted = blacklisted;
        if(!blacklisted)
          continue;
        await user.SendDMAsync("You have been blacklisted. The bot will no longer respond to your commands.");
      }
      await Db.Save();
      await Success();
    }

  }

}

}

