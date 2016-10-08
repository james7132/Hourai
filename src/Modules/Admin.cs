using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Hourai {

[Module]
[PublicOnly]
[ModuleCheck(ModuleType.Admin)]
public class Admin {

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
        await u.BanAsync();
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

  [Group("prune")]
  public class PruneGroup {

    [Command]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
    public Task Prune(IUserMessage msg, int count = 100) {
      return PruneMessages(Check.InGuild(msg), m => true, count);
    }

    [Command("user")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
    public Task PruneUser(IUserMessage msg, params IGuildUser[] users) {
      var userSet = new HashSet<IUser>(users);
      return PruneMessages(Check.InGuild(msg), m => userSet.Contains(m.Author));
    }

    [Command("embed")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages with embeds or attachments in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Embed(IUserMessage msg, int count = 100) {
      return PruneMessages(Check.InGuild(msg), m => m.Embeds.Any() || m.Attachments.Any(), count);
    }

    [Command("mine")]
    [Remarks("Removes all messages from the user using the command in the last X messages.")]
    public Task Mine(IUserMessage msg, int count = 100) {
      ulong id = msg.Author.Id;
      return PruneMessages(Check.InGuild(msg), m => m.Author.Id == id, count);
    }

    [Command("ping")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages that mentioned other users or roles the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Mention(IUserMessage msg, int count = 100) {
      return PruneMessages(Check.InGuild(msg), m => m.MentionedUsers.Any() || m.MentionedRoles.Any(), count);
    }

    [Command("bot")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all bots in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task PruneBot(IUserMessage msg, int count = 100) {
      return PruneMessages(Check.InGuild(msg), m => m.Author.IsBot, count);
    }

    static async Task PruneMessages(IMessageChannel channel,
                                  Func<IMessage, bool> pred = null,
                                  int count = 100) {
      if (count > Config.PruneLimit)
        count = Config.PruneLimit;
      if (count < 0) {
        await channel.Respond("Cannot prune a negative count of messages");
        return;
      }
      var finalCount = count;
      var messages = await channel.GetMessagesAsync(count);
      IEnumerable<IMessage> allMessages = messages;
      if (pred != null) {
        var filtered = messages.Where(pred).ToArray();
        finalCount = Math.Min(finalCount, filtered.Length);
        allMessages = filtered;
      }
      await channel.DeleteMessagesAsync(allMessages
                      .OrderByDescending(m => m.Timestamp)
                      .Take(count));
      await channel.Success($"Deleted {finalCount} messages.");
    }
  }

  [Group("channel")]
  public class ChannelGroup {

    [Command("create")]
    [Permission(GuildPermission.ManageChannels, Require.Both)]
    [Remarks("Creates a public channel with a specified name. Requires ``Manage Channels`` permission.")]
    public async Task Create(IUserMessage msg, string name) {
      var guild = Check.InGuild(msg).Guild;
      var channel = await guild.CreateTextChannelAsync(name); 
      await msg.Success($"{channel.Mention} created.");
    }

    [Command("delete")]
    [Permission(GuildPermission.ManageChannels, Require.Both)]
    [Remarks("Deletes all mentioned channels. Requires ``Manage Channels`` permission.")]
    public Task Delete(IUserMessage msg, params IGuildChannel[] channels) {
      return CommandUtility.ForEvery(msg, channels, CommandUtility.Action(
        async delegate(IGuildChannel channel) {
          await channel.DeleteAsync();
        }));
    }

    [Command("list")]
    [Remarks("Responds with a list of all text channels that the bot can see on this server.")]
    public async Task List(IUserMessage msg) {
      var guild = Check.InGuild(msg).Guild;
      var channels = (await guild.GetChannelsAsync()).OfType<ITextChannel>();
      await msg.Respond(channels.OrderBy(c => c.Position)
          .Select(c => c.Mention).Join(", "));
    }

    [Command("permissions")]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IUserMessage msg, IGuildUser user = null) {
      user = user ?? (msg.Author as IGuildUser);
      var perms = user.GetPermissions(Check.InGuild(msg));
      await msg.Respond(perms.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

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

  [Group("role")]
  public class RoleGroup {
    const string Requirement = " Requires ``Manage Role`` permission for both user and bot.";

    [Command("add")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Adds a role to all mentioned users." + Requirement)]
    public async Task Add(IUserMessage msg, IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(msg, "add role", async u => await u.AddRolesAsync(role));
      await CommandUtility.ForEvery(msg, users, action);
    }

    [Command("list")]
    [Remarks("Lists all roles on this server.")]
    public async Task List(IUserMessage msg) {
      var guild = Check.InGuild(msg).Guild;
      var roles = guild.Roles
        .Where(r => r.Id != guild.EveryoneRole.Id)
        .OrderBy(r => r.Position);
      await msg.Respond(roles.Select(r => r.Name).Join(", "));
    }

    [Command("remove")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users." + Requirement)]
    public async Task Remove(IUserMessage msg, IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(msg, "remove role", async u => await u.RemoveRolesAsync(role));
      await CommandUtility.ForEvery(msg, users, action);
    }

    [Command("nuke")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all users on the server." + Requirement)]
    public async Task Nuke(IUserMessage msg, params IRole[] roles) {
      var users = await Check.InGuild(msg).Guild.GetUsersAsync();
      var action = await CommandUtility.Action(msg, "remove role", async u => await u.RemoveRolesAsync(roles));
      await CommandUtility.ForEvery(msg, users, action);
    }

    [Command("ban")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Bans all mentioned users from a specified role." + Requirement)]
    public async Task RoleBan(IUserMessage msg, IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(msg, "ban",
        async u => {
          await u.RemoveRolesAsync(role);
          var guildUser = await Bot.Database.GetGuildUser(u);
          guildUser.BanRole(role);
        });
      await Bot.Database.Save();
      await CommandUtility.ForEvery(msg, users, action);
    }

    [Command("unban")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Unban all mentioned users from a specified role." + Requirement)]
    public async Task RoleUnban(IUserMessage msg, IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(msg, "ban",
        async u => {
          var guildUser = await Bot.Database.GetGuildUser(u);
          guildUser.UnbanRole(role);
        });
      await Bot.Database.Save();
      await CommandUtility.ForEvery(msg, users, action);
    }

    [Command("create")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Creates a mentionable role and applies it to all mentioned users")]
    public async Task RoleCreate(IUserMessage msg, string name) {
      var guild = Check.InGuild(msg).Guild;
      await guild.CreateRoleAsync(name);
      await msg.Success();
    }

    [Command("delete")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Deletes a role and removes it from all users.")]
    public Task RoleDelete(IUserMessage msg, params IRole[] roles) {
      return CommandUtility.ForEvery(msg, roles, CommandUtility.Action(
        async delegate(IRole role) {
          await role.DeleteAsync(); 
        }));
    }

    [Command("color")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Sets a role's color." + Requirement)]
    public async Task RoleColor(IUserMessage msg, string color, params IRole[] roles) {
      uint colorVal;
      if(!TryParseColor(color, out colorVal)) {
        await msg.Respond($"Could not parse {color} to a proper color value");
        return;
      }
      await Task.WhenAll(roles.Select(delegate(IRole role) {
              return role.ModifyAsync(r => {
                    r.Color = colorVal;
                  });
            }));
      await msg.Success();
    }

    [Command("rename")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Renames all mentioned roles" + Requirement)]
    public async Task Rename(IUserMessage msg, string name, params IRole[] roles) {
      await Task.WhenAll(roles.Select(delegate(IRole role) {
            return role.ModifyAsync(r => {
              r.Name = name;
            });
          }));
      await msg.Success();
    }

    bool TryParseColor(string color, out uint val) {
      if(uint.TryParse(color, 
            NumberStyles.HexNumber,
            null,
            out val))
        return true;
      return false;
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
