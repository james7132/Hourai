using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Hourai {

[ModuleCheck(ModuleType.Standard)]
public class Standard : HouraiModule {

  [Command("echo")]
  [Remarks("Has the bot repeat what you say")]
  public Task Echo([Remainder] string remainder) {
    return RespondAsync(remainder);
  }

  [Command("avatar")]
  [Remarks("Gets the avatar url of all mentioned users.")]
  public Task Avatar(params IGuildUser[] users) {
    IUser[] allUsers = users;
    if (users.Length <= 0)
      allUsers = new[] {Context.Message.Author};
    return RespondAsync(allUsers.Select(u => u.AvatarUrl).Join("\n"));
  }

  [Command("serverinfo")]
  [PublicOnly]
  [Remarks("Gets general information about the current server")]
  public async Task ServerInfo() {
    var builder = new StringBuilder();
    var server = Check.NotNull(Context?.Guild);
    var guild = await Bot.Database.GetGuild(server);
    var owner = await server.GetUserAsync(server.OwnerId);
    var channels = await server.GetChannelsAsync();
    var textChannels = channels.OfType<ITextChannel>().Order().Select(ch => ch.Name.Code());
    var voiceChannels = channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name.Code());
    var roles = server.Roles.Where(r => r.Id != server.EveryoneRole.Id);
    var users = await server.GetUsersAsync();
    builder.AppendLine($"Name: {server.Name.Code()}");
    builder.AppendLine($"ID: {server.Id.ToString().Code()}");
    builder.AppendLine($"Owner: {owner.Username.Code()}");
    builder.AppendLine($"Region: {server.VoiceRegionId.Code()}");
    builder.AppendLine($"Created: {server.CreatedAt.ToString().Code()}");
    builder.AppendLine($"User Count: {users.Count.ToString().Code()}");
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Order().Select(r => r.Name.Code()).Join(", ")}");
    builder.AppendLine($"Text Channels: {textChannels.Join(", ")}");
    builder.AppendLine($"Voice Channels: {voiceChannels.Join(", ")}");
    if(!string.IsNullOrEmpty(server.IconUrl))
      builder.AppendLine(server.IconUrl);
    await Context.Message.Respond(builder.ToString());
  }

  [Command("channelinfo")]
  [PublicOnly]
  [Remarks("Gets information on a specified channel")]
  public Task ChannelInfo(IGuildChannel channel = null) {
    if(channel == null)
      channel = Check.InGuild(Context.Message);
    return Context.Message.Respond($"ID: {channel.Id.ToString().Code()}");
  }

  [Command("whois")]
  [Remarks("Gets information on a specified users")]
  public Task WhoIs(IGuildUser user) {
    var builder = new StringBuilder();
    builder.AppendLine($"{Context.Message.Author.Mention}:");
    builder.AppendLine($"Username: {user.Username.Code()} {(user.IsBot ? "(BOT)".Code() : string.Empty )}");
    builder.AppendLine($"Nickname: {user.Nickname.NullIfEmpty()?.Code() ?? "N/A".Code()}");
    builder.AppendLine($"Current Game: {user.Game?.Name.Code() ?? "N/A".Code()}");
    builder.AppendLine($"ID: {user.Id.ToString().Code()}");
    builder.AppendLine($"Joined on: {user.JoinedAt?.ToString().Code() ?? "N/A".Code()}");
    builder.AppendLine($"Created on: {user.CreatedAt.ToString().Code()}");
    var roles = user.GetRoles();
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Select(r => r.Name.Code()).Join(", ")}");
    if(!string.IsNullOrEmpty(user.AvatarUrl))
      builder.AppendLine(user.AvatarUrl);
    return Context.Message.Channel.SendMessageAsync(builder.ToString());
  }

  [Command("topic")]
  [Remarks("Returns the mentioned channels' topics. If none are mentioned, the current channel is used.")]
  public Task Topic(params IGuildChannel[] channels) {
    if(channels.Length <= 0)
      channels = new[] { Context.Message.Channel as IGuildChannel };
    var builder = new StringBuilder();
    foreach(var channel in channels.OfType<ITextChannel>())
      builder.AppendLine($"{channel.Name}: {channel.Topic}");
    return Context.Message.Respond(builder.ToString());
  }
  

  [Group("search")]
  public class Search : HouraiModule {

    static Func<string, bool> ExactMatch(IEnumerable<string> matches) {
      return s => matches.All(s.Contains);
    }

    static Func<string, bool> RegexMatch(string regex) {
      return new Regex(regex, RegexOptions.Compiled).IsMatch;
    }

    [Command]
    [PublicOnly]
    [Remarks("Search the history of the current channel for messages that match all of the specfied search terms.")]
    public Task SearchChat(params string[] terms) {
      return SearchChannel(ExactMatch(terms));
    }

    [Command("regex")]
    [PublicOnly]
    [Remarks("Search the history of the current channel for matches to a specfied regex.")]
    public Task SearchRegex(string regex) {
      return SearchChannel(RegexMatch(regex));
    }

    [Command("day")]
    [PublicOnly]
    [Remarks("SearchChat the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
    public Task Day(string day) {
      var channel = Check.InGuild(Context.Message);
      string path = Bot.Get<LogService>().Logs.GetChannel(channel).GetPath(day);
      if (File.Exists(path))
        return Context.Message.SendFileRetry(path);
      else
        return RespondAsync($"A log for {channel.Mention} on date {day} cannot be found.");
    }

    [Command("ignore")]
    [PublicOnly]
    [Remarks("Mentioned channels will not be searched in ``search all``, except while in said channel. "
      + "User must have ``Manage Channels`` permission")]
    public Task Ignore(params IGuildChannel[] channels) {
      return SetIgnore(channels, true);
    }

    [Command("unigore")]
    [PublicOnly]
    [Remarks("Mentioned channels will appear in ``search all`` results." 
      +" User must have ``Manage Channels`` permission")]
    public Task Unignore(params IGuildChannel[] channels) {
      return SetIgnore(channels, false);
    }

    async Task SetIgnore(IEnumerable<IGuildChannel> channels, bool value) {
      var channel = Check.InGuild(Context.Message);
      foreach (var ch in channels) 
        (await Bot.Database.GetChannel(ch)).SearchIgnored = value;
      await Bot.Database.Save();
      await Success();
    }

    [Group("all")]
    public class All : HouraiModule {

      [Command]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server for any of the specfied search terms.")]
      public async Task SearchAll(params string[] terms) {
        await SearchAll(ExactMatch(terms));
      }

      [Command("regex")]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server based on a regex.")]
      public async Task SearchAllRegex(string regex) {
        await SearchAll(RegexMatch(regex));
      }

      async Task SearchAll(Func<string, bool> pred) {
        try {
          var channel = Check.InGuild(Context.Message);
          string reply = await Bot.Get<LogService>().Logs.GetChannel(channel).SearchAll(pred);
          await Context.Message.Respond(reply);
        } catch (Exception e) {
          Log.Error(e);
        }
      }
    }

    async Task SearchChannel(Func<string, bool> pred) {
      try {
        var channel = Check.InGuild(Context.Message);
        string reply = await Bot.Get<LogService>().Logs.GetChannel(channel).Search(pred);
        await Context.Message.Respond(reply);
      } catch(Exception e) {
        Log.Error(e);
      }
    }

  } 

  [Group("module")]
  public class Module : HouraiModule {

    static readonly Type HideType = typeof(HideAttribute);
    static readonly Type moduleType = typeof(ModuleCheckAttribute);
    IEnumerable<string> Modules => Bot.CommandService.Modules
      .Where(m => !m.Source.IsDefined(HideType, false) &&
           m.Source.IsDefined(moduleType, false))
      .Select(m => m.Name).ToList();

    [Command]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    [Remarks("Lists all modules available. Enabled ones are highligted. Requires user to have ``Manage Server`` permission.")]
    public async Task ModuleList() {
      var config = await Bot.Database.GetGuild(Check.NotNull(Context?.Guild));
      var modules = Enum.GetValues(typeof(ModuleType));
      await Context.Message.Respond(modules.OfType<ModuleType>()
          .Select(m => (config.IsModuleEnabled(m)) 
            ? m.ToString().Bold().Italicize() 
            : m.ToString())
          .Join(", "));
    }

    [Command("enable")]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    [Remarks("Enables a module for this server. Requires user to have ``Manage Server`` permission.")]
    public async Task ModuleEnable(params string[] modules) {
      var response = new StringBuilder();
      var config = await Bot.Database.GetGuild(Check.NotNull(Context?.Guild));
      foreach (var module in modules) {
        ModuleType type;
        if(Enum.TryParse(module, true, out type)) {
          config.AddModule(type);
          response.AppendLine($"{Config.SuccessResponse}: Module {module} enabled.");
        } else {
          response.AppendLine("Module {module} not found.");
        }
      }
      await Bot.Database.Save();
      await Context.Message.Respond(response.ToString());
    }

    [Command("disable")]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    [Remarks("Disable a module for this server. Requires user to have ``Manage Server`` permission.")]
    public async Task ModuleDisable(params string[]  modules) {
      var response = new StringBuilder();
      var config = await Bot.Database.GetGuild(Check.NotNull(Context?.Guild));
      foreach (var module in modules) {
        ModuleType type;
        if(Enum.TryParse(module, true, out type)) {
          config.RemoveModule(type);
          response.AppendLine($"{Config.SuccessResponse}: Module {module} disabled.");
        } else {
          response.AppendLine("Module {module} not found.");
        }
      }
      await Bot.Database.Save();
      await Context.Message.Respond(response.ToString());
    }
  }
}

}
