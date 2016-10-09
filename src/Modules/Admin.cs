using Discord;
using Discord.Commands;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

[Module]
[PublicOnly]
[ModuleCheck(ModuleType.Admin)]
public partial class Admin {

  [Command("kick")]
  [Permission(GuildPermission.KickMembers)]
  [Remarks("Kicks all mentioned users. Requires ``Kick Members`` permission.")]
  public async Task Kick(IUserMessage msg, 
                         params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "kick", u => u.KickAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("ban")]
  [Permission(GuildPermission.BanMembers)]
  [Remarks("Bans all mentioned users. Requires ``Ban Members`` permission.")]
  public async Task Ban(IUserMessage msg, 
                        params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "ban", u => u.BanAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("softban")]
  [Permission(GuildPermission.BanMembers)]
  [Remarks("Softbans all mentioned users. Requires ``Ban Members`` permission.")]
  public async Task Softban(IUserMessage msg, 
                            params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "ban", async u => {
        ulong id = u.Id;
        await u.BanAsync(7); // Prune 7 day's worth of messages
        await u.Guild.RemoveBanAsync(id);
      });
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("mute")]
  [Permission(GuildPermission.MuteMembers)]
  [Remarks("Server mutes all mentioned users. Requires ``Mute Members`` permission.")]
  public async Task Mute(IUserMessage msg, params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "mute", async u => await u.MuteAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("unmute")]
  [Permission(GuildPermission.MuteMembers)]
  [Remarks( "Server unmutes all mentioned users. Requires ``Mute Members`` permission.")]
  public async Task Unmute(IUserMessage msg, params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "unmute", async u => await u.UnmuteAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("deafen")]
  [Permission(GuildPermission.DeafenMembers)]
  [Remarks( "Server deafens all mentioned users. Requires ``Deafen Members`` permission.")]
  public async Task Deafen(IUserMessage msg, params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "deafen", async u => await u.DeafenAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("undeafen")]
  [Permission(GuildPermission.DeafenMembers)]
  [Remarks( "Server undeafens all mentioned users. Requires ``Deafen Members`` permission.")]
  public async Task Undeafen(IUserMessage msg, params IGuildUser[] users) {
    var action = await CommandUtility.Action(msg, "undeafen", async u => await u.UndeafenAsync());
    await CommandUtility.ForEvery(msg, users, action);
  }

  [Command("nickname")]
  [Remarks("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
  + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
  + "``Manage Nicknames`` permission.")]
  public async Task Nickname(IUserMessage msg, string nickname, params IGuildUser[] users) {
    Check.InGuild(msg);
    var author = msg.Author as IGuildUser;
    IGuildUser[] allUsers = users;
    if (allUsers.Length <= 0) {
      if(!author.GuildPermissions.ChangeNickname) {
        await msg.Respond($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{Config.CommandPrefix}help nickname``");
        return;
      }
      allUsers = new[] {msg.Author as IGuildUser};
    }
    if(!author.GuildPermissions.ManageNicknames) {
      await msg.Respond($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{Config.CommandPrefix}help nickname``");
      return;
    }

    var action = await CommandUtility.Action(msg, "nickname", async u => await u.SetNickname(nickname));
    await CommandUtility.ForEvery(msg, allUsers, action);
  }

  [Command("modlog")]
  [Remarks("Gets the most recent changes on the server")]
  public Task Modlog(IUserMessage msg) {
    var guild = Check.InGuild(msg).Guild;
    var log = Bot.Get<LogService>().Logs.GetGuild(guild);
    var path =  log.GetPath(DateTimeOffset.Now);
    if(File.Exists(path))
      return Utility.FileIO(() => msg.Channel.SendFileAsync(path));
    else
      return msg.Respond("No mod events logged thus far.");
  }

  [Group("server")]
  public class ServerGroup {

    [Command("permissions")]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IUserMessage msg, IGuildUser user = null) {
      user = user ?? (msg.Author as IGuildUser);
      await msg.Respond(user.GuildPermissions.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

  }

  static async Task RoleCommand(IUserMessage m, IRole role, string action, IEnumerable<IGuildUser> users, Func<IGuildUser, IRole, Task> task) {
    var guild = Check.InGuild(m).Guild;
    var selfUser = await Bot.Client.GetCurrentUserAsync();
    var guildBot = await guild.GetUserAsync(selfUser.Id);
    if (!Utility.RoleCheck(guildBot, role))
      throw new RoleRankException($"{guildBot.Username} cannot {action} role \"{role.Name}\", as it is above my roles.");
    if (!Utility.RoleCheck(m.Author as IGuildUser, role))
      throw new RoleRankException($"{m.Author.Username}, you cannot {action} role \"{role.Name}\", as it is above their roles.");
    await CommandUtility.ForEvery(m, users,
      await CommandUtility.Action(m, action + " role", user => task(user, role)));
    }
  }

}
