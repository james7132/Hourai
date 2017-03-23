using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Discord.Commands;
using Hourai.Model;
using Hourai.Custom;
using System;
using System.Collections.Generic;
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
    var reconnects = Client.Shards.Select(s => Counters.Get($"shard-{s.ShardId}-reconnects")?.Value ?? 0)
      .Aggregate(0UL, (a, c) => a + c);
    var table = new Table(2);
    const string all = "All";
    const string stored = "Stored";
    table[all, "Guilds"] = Client.Guilds.Count;
    table[all, "Users"] = users;
    table[all, "Guild Users"] = Client.Guilds.Sum(g => g.MemberCount);
    table[all, "Roles"] = Client.Guilds.Sum(g => g.Roles.Count);
    table[all, "Channels"] = Client.Guilds.Sum(g => g.Channels.Count);
    table[all, "Reconnects"] = reconnects;
    table[all, "Messages"] = Counters.Get("messages-recieved").Value;
    table[stored, "Guilds"] = Db.Guilds.Count();
    table[stored, "Users"] = Db.Users.Count();
    table[stored, "Guild Users"] = Db.GuildUsers.Count();
    table[stored, "Roles"] = Db.Roles.Count();
    table[stored, "Channels"] = Db.Channels.Count();

    if (Client.Shards.Count > 1) {
      foreach (var shard in Client.Shards)
        AddShard(shard, table);
    }

    builder.AppendLine("Stats".Bold());
    using (builder.MultilineCode()) {
      builder.AppendLine(table.ToString());
    }
    builder.AppendLine($"Start Time: {Bot.StartTime}")
      .AppendLine($"Uptime: {Bot.Uptime}")
      .AppendLine()
      .AppendLine($"Client: Discord .NET v{DiscordConfig.Version} (API v{DiscordConfig.APIVersion}, {DiscordSocketConfig.GatewayEncoding})")
      .AppendLine($"Latency: {Context.Client.Latency}ms")
      .AppendLine($"Total Memory Used: {BytesToMemoryValue(Process.GetCurrentProcess().WorkingSet64)}");
    await Context.Message.Respond(builder.ToString());
  }

  void AddShard(DiscordSocketClient client, Table table) {
    var users = (from guild in client.Guilds
                from user in guild.Users
                select user.Id).Distinct().Count();
    var name = "Shard " + client.ShardId;
    table[name, "Guilds"] = client.Guilds.Count;
    table[name, "Users"] = users;
    table[name, "Guild Users"] = client.Guilds.Sum(g => g.MemberCount);
    table[name, "Roles"] = client.Guilds.Sum(g => g.Roles.Count);
    table[name, "Channels"] = client.Guilds.Sum(g => g.Channels.Count);
    table[name, "Reconnects"] = Counters.Get($"shard-{client.ShardId}-reconnects")?.Value ?? 0;
    table[name, "Messages"] = Counters.Get($"shard-{client.ShardId}-messages-recieved").Value ?? 0;
  }

  [Group("refresh")]
  public class Refresh : HouraiModule {

    [Command]
    [RequireContext(ContextType.Guild)]
    public async Task RefreshGuild() {
      Log.Info($"Refreshing {Context.Guild.ToIDString()}...");
      try {
        Db.AllowSave = false;
        await Db.RefreshGuild(Context.Guild);
      } finally {
        Db.AllowSave = true;
        await Db.Save();
      }
      Log.Info("Done refreshing.");
      await Success();
    }

    [Command("all")]
    public async Task RefreshAll() {
      Log.Info("Starting refresh...");
      Db.AllowSave = false;
      try {
        await Task.WhenAll(Context.Client.Guilds.Select(guild => Db.RefreshGuild(guild)));
      } finally {
        Db.AllowSave = true;
        await Db.Save();
      }
      Log.Info("Done refreshing.");
      await Success();
    }

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
    using (var httpClient = new HttpClient()) {
        using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url))) {
            using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync()) {
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
      Context.DbGuild.IsBlacklisted = true;
      await Db.Save();
      await Success();
      await guild.LeaveAsync();
    }

    [Command("user")]
    [Remarks("Blacklist user(s) and prevent them from using commands.")]
    public async Task User(string setting, params IGuildUser[] users) {
      var blacklisted = SettingToBlacklist(setting);
      await Task.WhenAll(users.Select(async user => {
        var uConfig = await Db.Users.Get(user);
        if(uConfig == null)
          return;
        uConfig.IsBlacklisted = blacklisted;
        if(blacklisted)
          await user.SendDMAsync("You have been blacklisted. The bot will no longer respond to your commands.");
      }));
      await Db.Save();
      await Success();
    }

  }

}

}

