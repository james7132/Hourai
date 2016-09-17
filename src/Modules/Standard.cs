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

[Module]
[ModuleCheck(ModuleType.Standard)]
public class Standard {

  [Command("echo")]
  [Remarks("Has the bot repeat what you say")]
  public Task Echo(IUserMessage message, [Remainder] string remainder) {
    return message.Respond(remainder);
  }

  [Command("avatar")]
  [Remarks("Gets the avatar url of all mentioned users.")]
  public Task Avatar(IUserMessage message, params IGuildUser[] users) {
    IUser[] allUsers = users;
    if (users.Length <= 0)
      allUsers = new[] {message.Author};
    return message.Respond(allUsers.Select(u => u.AvatarUrl).Join("\n"));
  }

  [Command("serverinfo")]
  [Remarks("Gets general information about the current server")]
  public async Task ServerInfo(IUserMessage message) {
    var builder = new StringBuilder();
    var server = Check.InGuild(message).Guild;
    var guild = await Bot.Database.GetGuild(server);
    var owner = await server.GetUserAsync(server.OwnerId);
    var channels = await server.GetChannelsAsync();
    var textChannels = channels.OfType<ITextChannel>().Order().Select(ch => ch.Name.Code());
    var voiceChannels = channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name.Code());
    var roles = server.Roles.Where(r => r.Id != server.EveryoneRole.Id);
    builder.AppendLine($"Name: {server.Name.Code()}");
    builder.AppendLine($"ID: {server.Id.ToString().Code()}");
    builder.AppendLine($"Owner: {owner.Username.Code()}");
    builder.AppendLine($"Region: {server.VoiceRegionId.Code()}");
    builder.AppendLine($"Created: {server.CreatedAt.ToString().Code()}");
    builder.AppendLine($"User Count: {server.GetUserCount().ToString().Code()}");
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Order().Select(r => r.Name.Code()).Join(", ")}");
    builder.AppendLine($"Text Channels: {textChannels.Join(", ")}");
    builder.AppendLine($"Voice Channels: {voiceChannels.Join(", ")}");
    if(!string.IsNullOrEmpty(server.IconUrl))
      builder.AppendLine(server.IconUrl);
    await message.Respond(builder.ToString());
  }

  [Command("whois")]
  [Remarks("Gets information on a specified users")]
  public Task WhoIs(IUserMessage message, IGuildUser user) {
    var builder = new StringBuilder();
    builder.AppendLine($"{message.Author.Mention}:");
    builder.AppendLine($"Username: {user.Username.Code()} {(user.IsBot ? "(BOT)".Code() : string.Empty )}");
    builder.AppendLine($"Nickname: {user.Nickname.NullIfEmpty()?.Code() ?? "N/A".Code()}");
    builder.AppendLine($"Current Game: {user.Game?.Name.Code() ?? "N/A".Code()}");
    builder.AppendLine($"ID: {user.Id.ToString().Code()}");
    builder.AppendLine($"Joined on: {user.JoinedAt?.ToString().Code() ?? "N/A".Code()}");
    builder.AppendLine($"Created on: {user.CreatedAt.ToString().Code()}");
    var roles = user.Roles.Where(r => r.Id != user.Guild.EveryoneRole.Id);
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Select(r => r.Name.Code()).Join(", ")}");
    if(!string.IsNullOrEmpty(user.AvatarUrl))
      builder.AppendLine(user.AvatarUrl);
    return message.Channel.SendMessageAsync(builder.ToString());
  }

  [Command("topic")]
  [Remarks("Returns the mentioned channels' topics. If none are mentioned, the current channel is used.")]
  public Task Topic(IUserMessage msg, params IGuildChannel[] channels) {
    if(channels.Length <= 0)
      channels = new[] { msg.Channel as IGuildChannel };
    var builder = new StringBuilder();
    foreach(var channel in channels.OfType<ITextChannel>())
      builder.AppendLine($"{channel.Name}: {channel.Topic}");
    return msg.Respond(builder.ToString());
  }
  

  [Group("search")]
  public class Search {

    static Func<string, bool> ExactMatch(IEnumerable<string> matches) {
      return s => matches.All(s.Contains);
    }

    static Func<string, bool> RegexMatch(string regex) {
      return new Regex(regex, RegexOptions.Compiled).IsMatch;
    }

    [Command]
    [PublicOnly]
    [Remarks("Search the history of the current channel for messages that match all of the specfied search terms.")]
    public async Task SearchChat(IUserMessage message, params string[] terms) {
      await SearchChannel(message, ExactMatch(terms));
    }

    [Command("regex")]
    [PublicOnly]
    [Remarks("Search the history of the current channel for matches to a specfied regex.")]
    public async Task SearchRegex(IUserMessage message, string regex) {
      await SearchChannel(message, RegexMatch(regex));
    }

    [Command("day")]
    [PublicOnly]
    [Remarks("SearchChat the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
    public async Task Day(IUserMessage message, string day) {
      var channel = Check.InGuild(message);
      string path = Bot.Channels.Get(channel).GetPath(day);
      if (File.Exists(path))
        await message.SendFileRetry(path);
      else
        await message.Respond($"A log for {channel.Mention} on date {day} cannot be found.");
    }

    [Command("ignore")]
    [PublicOnly]
    [Remarks("Mentioned channels will not be searched in ``search all``, except while in said channel. "
      + "User must have ``Manage Channels`` permission")]
    public Task Ignore(IUserMessage msg, params IGuildChannel[] channels) {
      return SetIgnore(msg, channels, true);
    }

    [Command("unigore")]
    [PublicOnly]
    [Remarks("Mentioned channels will appear in ``search all`` results." 
      +" User must have ``Manage Channels`` permission")]
    public Task Unignore(IUserMessage msg, params IGuildChannel[] channels) {
      return SetIgnore(msg, channels, false);
    }

    async Task SetIgnore(IUserMessage message, IEnumerable<IGuildChannel> channels, bool value) {
      var channel = Check.InGuild(message);
      foreach (var ch in channels) 
        (await Bot.Database.GetChannel(ch)).SearchIgnored = value;
      await Bot.Database.Save();
      await message.Success();
    }

    [Group("all")]
    public class All {

      [Command]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server for any of the specfied search terms.")]
      public async Task SearchAll(IUserMessage message, params string[] terms) {
        await SearchAll(message, ExactMatch(terms));
      }

      [Command("regex")]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server based on a regex.")]
      public async Task SearchAllRegex(IUserMessage message, string regex) {
        await SearchAll(message, RegexMatch(regex));
      }

      async Task SearchAll(IUserMessage message, Func<string, bool> pred) {
        try {
          var channel = Check.InGuild(message);
          string reply = await Bot.Channels.Get(channel).SearchAll(pred);
          await message.Respond(reply);
        } catch (Exception e) {
          Log.Error(e);
        }
      }
    }

    async Task SearchChannel(IUserMessage message, Func<string, bool> pred) {
      var channel = Check.InGuild(message);
      string reply = await Bot.Channels.Get(channel).Search(pred);
      await message.Respond(reply);
      //await message.Respond($"Matches found in {channel.Name}:\n{reply}");
    }

  } 

  [Group("module")]
  public class Module {

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
    public async Task ModuleList(IUserMessage message) {
      var config = await Bot.Database.GetGuild(Check.InGuild(message).Guild);
      var modules = Enum.GetValues(typeof(ModuleType));
      await message.Respond(modules.OfType<ModuleType>()
          .Select(m => (config.IsModuleEnabled(m)) 
            ? m.ToString().Bold().Italicize() 
            : m.ToString())
          .Join(", "));
    }

    [Command("enable")]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    [Remarks("Enables a module for this server. Requires user to have ``Manage Server`` permission.")]
    public async Task ModuleEnable(IUserMessage message, params string[] modules) {
      var response = new StringBuilder();
      var config = await Bot.Database.GetGuild(Check.InGuild(message).Guild);
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
      await message.Respond(response.ToString());
    }

    [Command("disable")]
    [PublicOnly]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    [Remarks("Disable a module for this server. Requires user to have ``Manage Server`` permission.")]
    public async Task ModuleDisable(IUserMessage message, params string[]  modules) {
      var response = new StringBuilder();
      var config = await Bot.Database.GetGuild(Check.InGuild(message).Guild);
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
      await message.Respond(response.ToString());
    }
  }
}

}
