using Discord;
using Discord.Commands;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("role")]
  public class Roles : HouraiModule {
    const string Requirement = " Requires ``Manage Role`` permission for both user and bot.";

    [Command("add")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Adds a role to all mentioned users." + Requirement)]
    public async Task Add(IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(Context, "add role", async u => await u.AddRolesAsync(role));
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("list")]
    [Remarks("Lists all roles on this server.")]
    public async Task List() {
      var guild = Check.InGuild(Context.Message).Guild;
      var roles = guild.Roles
        .Where(r => r.Id != guild.EveryoneRole.Id)
        .OrderBy(r => r.Position);
      await RespondAsync(roles.Select(r => r.Name).Join(", "));
    }

    [Command("remove")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users." + Requirement)]
    public async Task Remove(IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(Context, "remove role", async u => await u.RemoveRolesAsync(role));
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("nuke")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all users on the server." + Requirement)]
    public async Task Nuke(params IRole[] roles) {
      var users = await Check.InGuild(Context.Message).Guild.GetUsersAsync();
      var action = await CommandUtility.Action(Context, "remove role", async u => await u.RemoveRolesAsync(roles));
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("ban")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Bans all mentioned users from a specified role." + Requirement)]
    public async Task RoleBan(IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(Context, "ban",
        async u => {
          await u.RemoveRolesAsync(role);
          var guildUser = await Bot.Database.GetGuildUser(u);
          guildUser.BanRole(role);
        });
      await Bot.Database.Save();
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("unban")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Unban all mentioned users from a specified role." + Requirement)]
    public async Task RoleUnban(IRole role, params IGuildUser[] users) {
      var action = await CommandUtility.Action(Context, "ban",
        async u => {
          var guildUser = await Bot.Database.GetGuildUser(u);
          guildUser.UnbanRole(role);
        });
      await Bot.Database.Save();
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("create")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Creates a mentionable role and applies it to all mentioned users")]
    public async Task RoleCreate(string name) {
      var guild = Check.InGuild(Context.Message).Guild;
      await guild.CreateRoleAsync(name);
      await Success();
    }

    [Command("delete")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Deletes a role and removes it from all users.")]
    public Task RoleDelete(params IRole[] roles) {
      return CommandUtility.ForEvery(Context, roles, CommandUtility.Action(
        async delegate(IRole role) {
          await role.DeleteAsync(); 
        }));
    }

    [Command("color")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Sets a role's color." + Requirement)]
    public async Task RoleColor(string color, params IRole[] roles) {
      uint colorVal;
      if(!TryParseColor(color, out colorVal)) {
        await RespondAsync($"Could not parse {color} to a proper color value");
        return;
      }
      await Task.WhenAll(roles.Select(delegate(IRole role) {
        return role.ModifyAsync(r => { r.Color = colorVal; });
      }));
      await Success();
    }

    [Command("rename")]
    [Permission(GuildPermission.ManageRoles)]
    [Remarks("Renames all mentioned roles" + Requirement)]
    public async Task Rename(string name, params IRole[] roles) {
      await Task.WhenAll(roles.Select(delegate(IRole role) {
          return role.ModifyAsync(r => { r.Name = name; });
        }));
      await Success();
    }

    bool TryParseColor(string color, out uint val) {
      return uint.TryParse(color, NumberStyles.HexNumber, null, out val);
    }
  }

}

}

