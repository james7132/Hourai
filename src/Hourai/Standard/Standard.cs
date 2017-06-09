using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Preconditions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hourai.Standard {

public partial class Standard : HouraiModule {

  public LogSet Logs { get; set; }

  const ImageFormat ImgFormat = ImageFormat.Auto;
  const ushort AvatarSize = 1024;

  [Command("echo")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Has the bot repeat what you say")]
  public Task Echo([Remainder] string remainder) => ReplyAsync(Context.Process(remainder));

  [Command("choose")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Chooses between several provided choices. Seperated by spaces. Quote choices with spaces in them.")]
  public Task Choose(params string[] choices) {
    if (choices.Length <= 0)
      return RespondAsync($"There is nothing to choose from!");
    return RespondAsync($"I choose {choices.SelectRandom()}!");
  }
  
  [Command("time")]
  [ChannelRateLimit(1, 1)]
  [Remarks("Command relating to calculations of date and time. All times listed compared to Greenwich Mean Time. Daylight savings time NOT shown.")]
  public Task TimeFrame(params string[] country) {
    Echo("WARNING: The time zone readings you may see below do not take into consideration if DST is in effect. If in doubt, double check.")
    if (country == "USA" or "US" or "United States")
      Echo("**-05:00:** Eastern Time Zone - New York, Charlotte, Miami") // server based in Eastern Time Zone
      Echo("**-06:00:** Central Time Zone - Omaha, Kansas City, Chicago")
      Echo("**-07:00:** Mountain Time Zone - Denver, Helena")
      Echo("**-08:00:** Pacific Time Zone - San Francisco, Seattle, Las Vegas")
      Echo("**-09:00:** Alaska Time Zone - Anchorage")
      Echo("**-10:00:** Hawaii Time Zone - Honolulu, Aleutian Islands")
    else if (country == "Japan" or "JAP" or "JP")
      Echo("**+09:00:** Japan Standard - Tokyo, Kyoto")
    else if (country == "Canada" or "CAN")
      Echo("**-03:30:** Newfoundland Time Zone - Newfoundland")
      Echo("**-04:00:** Atlantic Time Zone")
      Echo("**-05:00:** Eastern Time Zone - Toronto")
      Echo("**-06:00:** Central Time Zone - Winnipeg")
      Echo("**-07:00:** Mountain Time Zone")
      Echo("**-08:00:** Pacific Time Zone")
    else if (country == "Russia" or "RUS" or "Russian Federation" or "RU") // because why not
      Echo("**+02:00:** Eastern European Zone - Kaliningrad")
      Echo("**+03:00:** Moscow Standard Time - Moscow")
      Echo("**+04:00:** Samara")
      Echo("**+05:00:** Yekaterinburg")
      Echo("**+06:00:** Omsk")
      Echo("**+07:00:** 'Middle Siberian Time Zone' - Krasnoyarsk, Novosibirsk")
      Echo("**+08:00:** Irkutsk")
      Echo("**+09:00:** Chita")
      Echo("**+10:00:** Vladvistok")
      Echo("**+11:00:** 'Russian Pacific Time Zone' - Magadan, Sakalin, Srednokolymsk")
      Echo("**+12:00:** Kamchatka Time Zone - Kamchatka, Anadyr")
    else
      Echo("No time zone posted for your country, or you typed it in wrong. Hourai accepts names like *United States* or *Russia*. Check back soon for updates.")
  
  

  [Command("avatar")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Gets the avatar url of the provided users. If no user is provided, your avatar is shown instead.")]
  public Task Avatar(params IGuildUser[] users) {
    IUser[] allUsers = users;
    if (users.Length <= 0)
      allUsers = new[] {Context.Message.Author};
    return RespondAsync(allUsers.Select(u => u.GetAvatarUrl(ImgFormat, AvatarSize)).Join("\n"));
  }

  [Command("invite")]
  [ChannelRateLimit(1, 1)]
  [Remarks("Provides a invite link to add this bot to your server")]
  public Task Invite() =>
    RespondAsync("Use this link to add me to your server: https://discordapp.com/oauth2/authorize?client_id=208460637368614913&scope=bot&permissions=0xFFFFFFFFFFFF");

  [Command("playing")]
  [ChannelRateLimit(1, 1)]
  [RequireContext(ContextType.Guild)]
  [Remarks("Gets all users currently playing a certain game.")]
  public async Task IsPlaying([Remainder] string game) {
    var guild = Check.NotNull(Context.Guild);
    var regex = new Regex(game, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    var players = from user in guild.Users
                  where user.Game?.Name != null && regex.IsMatch(user.Game?.Name)
                  group user.Username by user.Game.Value.Name into g
                  select $"{g.Key.Bold()}: {g.Join(", ")}";
    var results = players.Join("\n");
    await RespondAsync(!string.IsNullOrEmpty(results) ? results : "No results.");
  }

  static string TimeSummary(DateTimeOffset? time) {
    if (time == null)
      return "N/A";
    var timespan = DateTimeOffset.UtcNow - time.Value;
    if (timespan.TotalDays > 365.0)
      return $"{time} ({timespan.TotalDays/365:0.00} years ago)";
    if (timespan.TotalDays > 1.0)
      return $"{time} ({timespan.TotalDays:0.00} days ago)";
    if (timespan.TotalHours > 1.0)
      return $"{time} ({timespan.TotalHours:0.00} hours ago)";
    if (timespan.TotalMinutes > 1.0)
      return $"{time} ({timespan.TotalMinutes:0.00} minutes ago)";
    if (timespan.TotalSeconds > 1.0)
      return $"{time} ({timespan.TotalSeconds:0.00} seconds ago)";
    return $"{time} (moments ago)";
  }

  [Command("serverinfo")]
  [ChannelRateLimit(3, 1)]
  [RequireContext(ContextType.Guild)]
  [Remarks("Gets general information about the current server")]
  public async Task ServerInfo() {
    var builder = new StringBuilder();
    var server = Check.NotNull(Context.Guild);
    var owner = await server.GetOwner();
    var textChannels = server.Channels.OfType<ITextChannel>().Order().Select(ch => ch.Name.Code());
    var voiceChannels = server.Channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name.Code());
    var roles = server.Roles.Where(r => r.Id != server.EveryoneRole.Id);
    builder.AppendLine($"Name: {server.Name.Code()}")
      .AppendLine($"ID: {server.Id.ToString().Code()}")
      .AppendLine($"Owner: {owner.Username.Code()}")
      .AppendLine($"Region: {server.VoiceRegionId.Code()}")
      .AppendLine($"Created: {TimeSummary(server.CreatedAt)}")
      .AppendLine($"User Count: {server.MemberCount.ToString().Code()}");
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Order().Select(r => r.Name.Code()).Join(", ")}");
    builder.AppendLine($"Text Channels: {textChannels.Join(", ")}")
      .AppendLine($"Voice Channels: {voiceChannels.Join(", ")}")
      .AppendLine($"Bot Command Prefix: {Context.DbGuild.Prefix}");
    if(!string.IsNullOrEmpty(server.IconUrl))
      builder.AppendLine(server.IconUrl);
    await Context.Message.Respond(builder.ToString());
  }

  [Command("whois")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Gets information on a specified users")]
  public async Task WhoIs(IUser user) {
    const int spacing = 80;
    var guildUser = user as IGuildUser;
    var builder = new StringBuilder()
      .AppendLine($"Username: ``{user.Username}#{user.Discriminator}`` {(user.IsBot ? "(BOT)".Code() : string.Empty )} ({user.Id})");
    if (guildUser != null && !string.IsNullOrWhiteSpace(guildUser.Nickname))
      builder.AppendLine($"Nickname: {guildUser.Nickname.Code()}");
    if (user?.Game?.Name != null)
      builder.AppendLine($"Game: {user.Game?.Name.Code()}");
    builder.AppendLine($"Created on: {TimeSummary(user.CreatedAt).Code()}");
    if (guildUser != null)
      builder.AppendLine($"Joined on: {TimeSummary(guildUser.JoinedAt).Code()}");
    var count = Client.Guilds.Count(g => g.GetUser(user.Id) != null);
    var bans = await Task.WhenAll(from guild in Client.Guilds
                                  where guild.CurrentUser.GuildPermissions.BanMembers
                                  select guild.GetBansAsync());
    var banCount = bans.Count(banSet => banSet.Any(b => b.User.Id == user.Id));
    if (count > 1)
        builder.AppendLine($"Seen on **{count - 1}** other servers.");
    if (banCount > 1)
      builder.AppendLine($"Banned from **{banCount - 1}** other servers.");
    if (guildUser != null) {
      var roles = guildUser.GetRoles().Where(r => r.Id != guildUser.Guild.EveryoneRole.Id);
      if(roles.Any())
        builder.AppendLine($"Roles: {roles.Select(r => r.Name.Code()).Join(", ")}");
    }
    var avatar = user.GetAvatarUrl(ImgFormat, AvatarSize);
    if(!string.IsNullOrEmpty(avatar))
      builder.AppendLine(avatar);
    var usernames = await (from username in Db.Usernames
                           where username.UserId == user.Id && username.Name != user.Username
                           orderby username.Date descending
                           select username).ToListAsync();
    if(usernames.Any()) {
      using(builder.MultilineCode()) {
        foreach(var username in usernames) {
          builder.Append(username.Name.PadRight(spacing))
            .AppendLine(username.Date.ToString("yyyy-MM-dd"));
        }
      }
    }
    await RespondAsync(builder.ToString());
  }

  [Command("topic")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Returns the mentioned channels' topics. If none are mentioned, the current channel is used.")]
  public Task Topic(params IGuildChannel[] channels) {
    if(channels.Length <= 0)
      channels = new[] { Context.Channel as IGuildChannel };
    var builder = new StringBuilder();
    foreach(var channel in channels.OfType<ITextChannel>())
      builder.AppendLine($"{channel.Name}: {channel.Topic}");
    return Context.Message.Respond(builder.ToString());
  }

  [Group("hash")]
  public class Hash : HouraiModule {

    [Command("md5")]
    public async Task Md5([Remainder] string input = "") {
      using (var md5 = MD5.Create()) {
        await HashMessage(input, md5);
      }
    }

    [Command("sha1")]
    [Alias("sha-1")]
    public async Task Sha1([Remainder] string input = "") {
      using (var sha = SHA1.Create()) {
        await HashMessage(input, sha);
      }
    }

    [Command("sha256")]
    [Alias("sha-256")]
    public async Task Sha256([Remainder] string input = "") {
      using (var sha = SHA256.Create()) {
        await HashMessage(input, sha);
      }
    }

    [Command("sha384")]
    [Alias("sha-384")]
    public async Task Sha384([Remainder] string input = "") {
      using (var sha = SHA384.Create()) {
        await HashMessage(input, sha);
      }
    }

    [Command("sha512")]
    [Alias("sha-512")]
    public async Task Sha512([Remainder] string input = "") {
      using (var sha = SHA512.Create()) {
        await HashMessage(input, sha);
      }
    }

    static string GetURL(HouraiContext context, string input) {
      return context.Message.Embeds.SingleOrDefault()?.Url ??
        context.Message.Attachments.SingleOrDefault()?.Url;
    }

    static async Task<string> GetHash(string input, string url, HashAlgorithm hashing) {
      byte[] hash;
      if (url == null) {
        hash = hashing.ComputeHash(Encoding.ASCII.GetBytes(input));
      } else {
        using (var httpClient = new HttpClient()) {
          using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url))) {
            using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync()) {
              hash = hashing.ComputeHash(contentStream);
            }
          }
        }
      }
      return Convert.ToBase64String(hash);
    }

    async Task HashMessage(string input, HashAlgorithm hashing) {
      var url = GetURL(Context, input);
      await RespondAsync(await GetHash(input, url, hashing));
    }

    [Group("check")]
    public class Verify : HouraiModule {

      [Command("md5")]
      public async Task Md5(string hash, [Remainder] string input = "") {
        using (var md5 = MD5.Create()) {
          await CheckMessage(input, hash, md5);
        }
      }

      [Command("sha1")]
      [Alias("sha-1")]
      public async Task Sha1(string hash, [Remainder] string input = "") {
        using (var sha = SHA1.Create()) {
          await CheckMessage(input, hash, sha);
        }
      }

      [Command("sha256")]
      [Alias("sha-256")]
      public async Task Sha256(string hash, [Remainder] string input = "") {
        using (var sha = SHA256.Create()) {
          await CheckMessage(input, hash, sha);
        }
      }

      [Command("sha384")]
      [Alias("sha-384")]
      public async Task Sha384(string hash, [Remainder] string input = "") {
        using (var sha = SHA384.Create()) {
          await CheckMessage(input, hash, sha);
        }
      }

      [Command("sha512")]
      [Alias("sha-512")]
      public async Task Sha512(string hash, [Remainder] string input = "") {
        using (var sha = SHA512.Create()) {
          await CheckMessage(input, hash, sha);
        }
      }

      async Task CheckMessage(string input, string hash, HashAlgorithm hashing) {
        var url = GetURL(Context, input);
        var hashCheck = await GetHash(input, url, hashing);
        if (hash == hashCheck)
          await Success();
        else {
          await RespondAsync($":x: Hash `{hashCheck}` does not match `{hash}`");
        }
      }
    }

  }

}

}
