using Discord;
using Discord.Commands;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

public partial class Admin {

  [Group("role")]
  public class Roles : DatabaseHouraiModule {

    public Roles(BotDbContext db) : base(db) {
    }

    [Command("add")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Adds a role to all mentioned users.")]
    public async Task Add(IRole role, params IGuildUser[] users) {
      var action = CommandUtility.Action(async u => await u.AddRolesAsync(role));
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("list")]
    [Remarks("Lists all roles on this server.")]
    public async Task List() {
      var guild = Check.NotNull(Context.Guild);
      var roles = guild.Roles
        .Where(r => r.Id != guild.EveryoneRole.Id)
        .OrderBy(r => r.Position);
      await RespondAsync(roles.Select(r => r.Name).Join(", "));
    }

    [Command("remove")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users.")]
    public async Task Remove(IRole role, params IGuildUser[] users) {
      var action = CommandUtility.Action(async u => await u.RemoveRolesAsync(role));
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("nuke")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes all roles from provided users.")]
    public async Task Nuke(params IGuildUser[] users) {
      var action = CommandUtility.Action(async u => await u.RemoveRolesAsync());
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("ban")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Bans all mentioned users from a specified role.")]
    public async Task RoleBan(IRole role, params IGuildUser[] users) {
      var action = CommandUtility.Action(
        async u => {
          await u.RemoveRolesAsync(role);
          var guildUser = Database.GetGuildUser(u);
          guildUser.BanRole(role);
        });
      await Database.Save();
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("unban")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Unban all mentioned users from a specified role.")]
    public async Task RoleUnban(IRole role, params IGuildUser[] users) {
      var action = CommandUtility.Action(
        u => {
          var guildUser = Database.GetGuildUser(u);
          guildUser.UnbanRole(role);
          return Task.CompletedTask;
        });
      await Database.Save();
      await CommandUtility.ForEvery(Context, users, action);
    }

    [Command("create")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Creates a mentionable role and applies it to all mentioned users")]
    public async Task RoleCreate(string name) {
      await Check.NotNull(Context.Guild).CreateRoleAsync(name);
      await Success();
    }

    [Command("delete")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Deletes a role and removes it from all users.")]
    public Task RoleDelete(params IRole[] roles) {
      return CommandUtility.ForEvery(Context, roles, CommandUtility.Action(
        async delegate(IRole role) {
          await role.DeleteAsync();
        }));
    }

    [Command("color")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Sets a role's color.")]
    public async Task RoleColor(string color, params IRole[] roles) {
      uint colorVal;
      if(!TryParseColor(color, out colorVal)) {
        await RespondAsync($"Could not parse {color} to a proper color value");
        return;
      }
      await Task.WhenAll(roles.Select(role => {
        return role.ModifyAsync(r => { r.Color = colorVal; });
      }));
      await Success();
    }

    [Command("rename")]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Renames all mentioned roles")]
    public async Task Rename(string name, params IRole[] roles) {
      await Task.WhenAll(roles.Select(role => {
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

