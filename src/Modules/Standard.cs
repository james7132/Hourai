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
using Hourai.Preconditions;

namespace Hourai.Modules {

[RequireModule(ModuleType.Standard)]
public partial class Standard : DatabaseHouraiModule {

  LogSet Logs { get; }

  public Standard(BotDbContext db, LogSet logs) : base(db) {
    Logs = logs;
  }

  [Command("echo")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Has the bot repeat what you say")]
  public Task Echo([Remainder] string remainder) {
    return RespondAsync(remainder);
  }

  [Command("avatar")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Gets the avatar url of all mentioned users.")]
  public Task Avatar(params IGuildUser[] users) {
    IUser[] allUsers = users;
    if (users.Length <= 0)
      allUsers = new[] {Context.Message.Author};
    return RespondAsync(allUsers.Select(u => u.AvatarUrl).Join("\n"));
  }

  [Command("serverinfo")]
  [ChannelRateLimit(3, 1)]
  [RequireContext(ContextType.Guild)]
  [Remarks("Gets general information about the current server")]
  public async Task ServerInfo() {
    var builder = new StringBuilder();
    var server = Check.NotNull(Context.Guild);
    var guild = Database.GetGuild(server);
    var owner = await server.GetUserAsync(server.OwnerId);
    var channels = await server.GetChannelsAsync();
    var textChannels = channels.OfType<ITextChannel>().Order().Select(ch => ch.Name.Code());
    var voiceChannels = channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name.Code());
    var roles = server.Roles.Where(r => r.Id != server.EveryoneRole.Id);
    var socketServer = server as SocketGuild;
    var userCount = socketServer?.MemberCount ?? (await server.GetUsersAsync()).Count;
    builder.AppendLine($"Name: {server.Name.Code()}")
      .AppendLine($"ID: {server.Id.ToString().Code()}")
      .AppendLine($"Owner: {owner.Username.Code()}")
      .AppendLine($"Region: {server.VoiceRegionId.Code()}")
      .AppendLine($"Created: {server.CreatedAt.ToString().Code()}")
      .AppendLine($"User Count: {userCount.ToString().Code()}");
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Order().Select(r => r.Name.Code()).Join(", ")}");
    builder.AppendLine($"Text Channels: {textChannels.Join(", ")}")
      .AppendLine($"Voice Channels: {voiceChannels.Join(", ")}")
      .AppendLine($"Bot Command Prefix: {guild.Prefix}");
    if(!string.IsNullOrEmpty(server.IconUrl))
      builder.AppendLine(server.IconUrl);
    await Context.Message.Respond(builder.ToString());
  }

  [Command("channelinfo")]
  [ChannelRateLimit(3, 1)]
  [RequireContext(ContextType.Guild)]
  [Remarks("Gets information on a specified channel")]
  public Task ChannelInfo(IGuildChannel channel = null) {
    if(channel == null)
      channel = Check.InGuild(Context.Message);
    return Context.Message.Respond($"ID: {channel.Id.ToString().Code()}");
  }

  [Command("whois")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Gets information on a specified users")]
  public Task WhoIs(IGuildUser user) {
    const int spacing = 120;
    var dbUser = Database.GetUser(user);
    var builder = new StringBuilder()
      .AppendLine($"{Context.Message.Author.Mention}:")
      .AppendLine($"Username: {user.Username.Code()} {(user.IsBot ? "(BOT)".Code() : string.Empty )}")
      .AppendLine($"Nickname: {user.Nickname.NullIfEmpty()?.Code() ?? "N/A".Code()}")
      .AppendLine($"Current Game: {user.Game?.Name.Code() ?? "N/A".Code()}")
      .AppendLine($"ID: {user.Id.ToString().Code()}")
      .AppendLine($"Joined on: {user.JoinedAt?.ToString().Code() ?? "N/A".Code()}")
      .AppendLine($"Created on: {user.CreatedAt.ToString().Code()}");
    var roles = user.GetRoles().Where(r => r.Id != user.Guild.EveryoneRole.Id);
    if(roles.Any())
      builder.AppendLine($"Roles: {roles.Select(r => r.Name.Code()).Join(", ")}");
    if(!string.IsNullOrEmpty(user.AvatarUrl))
      builder.AppendLine(user.AvatarUrl);
    var usernames = dbUser.Usernames.Where(u => u.Name != user.Username);
    if(usernames.Any()) {
      using(builder.MultilineCode()) {
        foreach(var username in usernames.OrderByDescending(u => u.Date)) {
          builder.Append(username.Name);
          builder.Append(new string(' ', spacing - username.Name.Length));
          builder.AppendLine(username.Date.ToString("yyyy-MM-dd"));
        }
      }
    }
    return Context.Message.Channel.SendMessageAsync(builder.ToString());
  }

  [Command("topic")]
  [ChannelRateLimit(3, 1)]
  [Remarks("Returns the mentioned channels' topics. If none are mentioned, the current channel is used.")]
  public Task Topic(params IGuildChannel[] channels) {
    if(channels.Length <= 0)
      channels = new[] { Context.Message.Channel as IGuildChannel };
    var builder = new StringBuilder();
    foreach(var channel in channels.OfType<ITextChannel>())
      builder.AppendLine($"{channel.Name}: {channel.Topic}");
    return Context.Message.Respond(builder.ToString());
  }


  [Group("module")]
  [RequireContext(ContextType.Guild)]
  [RequirePermission(GuildPermission.ManageGuild, Require.User)]
  public class Module : DatabaseHouraiModule {

    CommandService Commands { get; }

    public Module(CommandService commands, BotDbContext db) : base(db) {
      Commands = commands;
    }

    IEnumerable<string> Modules => Commands.Modules
      .Select(m => m.Name).ToList();

    [Command]
    [ChannelRateLimit(3, 1)]
    [Remarks("Lists all modules available. Enabled ones are highligted.")]
    public async Task ModuleList() {
      var config = Database.GetGuild(Check.NotNull(Context.Guild));
      var modules = Enum.GetValues(typeof(ModuleType));
      await Context.Message.Respond(modules.OfType<ModuleType>()
          .Select(m => (config.IsModuleEnabled(m))
            ? m.ToString().Bold().Italicize()
            : m.ToString())
          .Join(", "));
    }

    [Command("enable")]
    [GuildRateLimit(2, 60)]
    [Remarks("Enables a module for this server.")]
    public async Task ModuleEnable(params string[] modules) {
      var response = new StringBuilder();
      var config = Database.GetGuild(Check.NotNull(Context.Guild));
      foreach (var module in modules) {
        ModuleType type;
        if(Enum.TryParse(module, true, out type)) {
          config.AddModule(type);
          response.AppendLine($"{Config.SuccessResponse}: Module {module} enabled.");
        } else {
          response.AppendLine("Module {module} not found.");
        }
      }
      await Database.Save();
      await Context.Message.Respond(response.ToString());
    }

    [Command("disable")]
    [GuildRateLimit(2, 60)]
    [Remarks("Disable a module for this server.")]
    public async Task ModuleDisable(params string[]  modules) {
      var response = new StringBuilder();
      var config = Database.GetGuild(Check.NotNull(Context.Guild));
      foreach (var module in modules) {
        ModuleType type;
        if(Enum.TryParse(module, true, out type)) {
          config.RemoveModule(type);
          response.AppendLine($"{Config.SuccessResponse}: Module {module} disabled.");
        } else {
          response.AppendLine("Module {module} not found.");
        }
      }
      await Database.Save();
      await Context.Message.Respond(response.ToString());
    }
  }
}

}
