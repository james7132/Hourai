using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

[PublicOnly]
[ModuleCheck(ModuleType.Admin)]
public partial class Admin : HouraiModule {

  LogSet Logs { get; }
  DiscordSocketClient Client { get; }

  public Admin(DiscordSocketClient client, LogSet log) {
    Client = client;
    Logs = log;
  }

  [Command("kick")]
  [Permission(GuildPermission.KickMembers)]
  [Remarks("Kicks all mentioned users. Requires ``Kick Members`` permission.")]
  public async Task Kick(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "kick", u => u.KickAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("ban")]
  [Permission(GuildPermission.BanMembers)]
  [Remarks("Bans all mentioned users. Requires ``Ban Members`` permission.")]
  public async Task Ban(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "ban", u => u.BanAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("softban")]
  [Permission(GuildPermission.BanMembers)]
  [Remarks("Softbans all mentioned users. Requires ``Ban Members`` permission.")]
  public async Task Softban(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "ban", async u => {
        ulong id = u.Id;
        await u.BanAsync(7); // Prune 7 day's worth of messages
        await u.Guild.RemoveBanAsync(id);
      });
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("mute")]
  [Permission(GuildPermission.MuteMembers)]
  [Remarks("Server mutes all mentioned users. Requires ``Mute Members`` permission.")]
  public async Task Mute(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "mute", async u => await u.MuteAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("unmute")]
  [Permission(GuildPermission.MuteMembers)]
  [Remarks( "Server unmutes all mentioned users. Requires ``Mute Members`` permission.")]
  public async Task Unmute(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "unmute", async u => await u.UnmuteAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("deafen")]
  [Permission(GuildPermission.DeafenMembers)]
  [Remarks( "Server deafens all mentioned users. Requires ``Deafen Members`` permission.")]
  public async Task Deafen(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "deafen", async u => await u.DeafenAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("undeafen")]
  [Permission(GuildPermission.DeafenMembers)]
  [Remarks( "Server undeafens all mentioned users. Requires ``Deafen Members`` permission.")]
  public async Task Undeafen(params IGuildUser[] users) {
    var action = await CommandUtility.Action(Context, "undeafen", async u => await u.UndeafenAsync());
    await CommandUtility.ForEvery(Context, users, action);
  }

  [Command("nickname")]
  [Remarks("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
  + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
  + "``Manage Nicknames`` permission.")]
  public async Task Nickname(string nickname, params IGuildUser[] users) {
    Check.NotNull(Context.Guild);
    var author = Context.Message.Author as IGuildUser;
    IGuildUser[] allUsers = users;
    if (allUsers.Length <= 0) {
      if(!author.GuildPermissions.ChangeNickname) {
        await RespondAsync($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{Config.CommandPrefix}help nickname``");
        return;
      }
      allUsers = new[] { author };
    }
    if(!author.GuildPermissions.ManageNicknames) {
      await RespondAsync($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{Config.CommandPrefix}help nickname``");
      return;
    }

    var action = await CommandUtility.Action(Context, "nickname", async u => await u.SetNickname(nickname));
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

  async Task RoleCommand(CommandContext context, IRole role, string action, IEnumerable<IGuildUser> users, Func<IGuildUser, IRole, Task> task) {
    var guild = Check.NotNull(context.Guild);
    var selfUser = Client.CurrentUser;
    var guildBot = await guild.GetUserAsync(selfUser.Id);
    var message = context.Message;
    if (!Utility.RoleCheck(guildBot, role))
      throw new RoleRankException($"{guildBot.Username} cannot {action} role \"{role.Name}\", as it is above my roles.");
    if (!Utility.RoleCheck(message.Author as IGuildUser, role))
      throw new RoleRankException($"{message.Author.Username}, you cannot {action} role \"{role.Name}\", as it is above their roles.");
    await CommandUtility.ForEvery(context, users,
      await CommandUtility.Action(context, action + " role", user => task(user, role)));
    }
  }

}
