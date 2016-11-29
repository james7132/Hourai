using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

[RequireContext(ContextType.Guild)]
[RequireModule(ModuleType.Admin)]
public partial class Admin : HouraiModule {

  LogSet Logs { get; }
  DiscordSocketClient Client { get; }
  BotDbContext Database { get; }

  public Admin(DiscordSocketClient client, LogSet log, BotDbContext db) {
    Client = client;
    Logs = log;
    Database = db;
  }

  [Command("kick")]
  [RequirePermission(GuildPermission.KickMembers)]
  [Remarks("Kicks all mentioned users.")]
  public async Task Kick(params IGuildUser[] users) {
    var action = CommandUtility.Action(u => u.KickAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("ban")]
  [RequirePermission(GuildPermission.BanMembers)]
  [Remarks("Bans all mentioned users.")]
  public async Task Ban(params IGuildUser[] users) {
    var action = CommandUtility.Action(u => u.BanAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("softban")]
  [RequirePermission(GuildPermission.BanMembers)]
  [Remarks("Softbans all mentioned users.")]
  public async Task Softban(params IGuildUser[] users) {
    var action = CommandUtility.Action(async u => {
        ulong id = u.Id;
        await u.BanAsync(7); // Prune 7 day's worth of messages
        await u.Guild.RemoveBanAsync(id);
      });
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("mute")]
  [RequirePermission(GuildPermission.MuteMembers)]
  [Remarks("Server mutes all mentioned users.")]
  public async Task Mute(params IGuildUser[] users) {
    var action = CommandUtility.Action(async u => await u.MuteAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("unmute")]
  [RequirePermission(GuildPermission.MuteMembers)]
  [Remarks( "Server unmutes all mentioned users.")]
  public async Task Unmute(params IGuildUser[] users) {
    var action = CommandUtility.Action(async u => await u.UnmuteAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("deafen")]
  [RequirePermission(GuildPermission.DeafenMembers)]
  [Remarks( "Server deafens all mentioned users.")]
  public async Task Deafen(params IGuildUser[] users) {
    var action = CommandUtility.Action(async u => await u.DeafenAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("undeafen")]
  [RequirePermission(GuildPermission.DeafenMembers)]
  [Remarks( "Server undeafens all mentioned users. ")]
  public async Task Undeafen(params IGuildUser[] users) {
    var action = CommandUtility.Action(async u => await u.UndeafenAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("nickname")]
  [Remarks("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
  + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
  + "``Manage Nicknames`` permission.")]
  public async Task Nickname(string nickname, params IGuildUser[] users) {
    var guild = Database.GetGuild(Context.Guild);
    var author = Context.Message.Author as IGuildUser;
    IGuildUser[] allUsers = users;
    if (allUsers.Length <= 0) {
      if(!author.GuildPermissions.ChangeNickname) {
        await RespondAsync($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{guild.Prefix}help nickname``");
        return;
      }
      allUsers = new[] { author };
    }
    if(!author.GuildPermissions.ManageNicknames) {
      await RespondAsync($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{guild.Prefix}help nickname``");
      return;
    }

    var action = CommandUtility.Action(async u => await u.SetNickname(nickname));
    await CommandUtility.ForEvery(Context, allUsers, action);
  }

  [Command("modlog")]
  [Remarks("Gets the most recent changes on the server")]
  public Task Modlog() {
    try  {
      var guild = Check.NotNull(Context.Guild);
      var log = Logs.GetGuild(guild);
      var path =  log.GetPath(DateTimeOffset.Now);
      if(File.Exists(path))
        return Utility.FileIO(() => Context.Channel.SendFileAsync(path));
      else
        return RespondAsync("No mod events logged thus far.");
    } catch(Exception e) {
      Log.Error("hello");
      Log.Error(e);
    }
    Log.Error("Done");
    return Task.CompletedTask;
  }

  [Group("server")]
  public class ServerGroup : HouraiModule {

    [Command("permissions")]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IGuildUser user = null) {
      user = user ?? (Context.Message.Author as IGuildUser);
      await RespondAsync(user.GuildPermissions.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

  }

}

}
