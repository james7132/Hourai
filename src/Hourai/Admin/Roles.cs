using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Preconditions;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

public partial class Admin {

  [Group("role")]
  public class Roles : HouraiModule {

    [Log]
    [Command("add")]
    [GuildRateLimit(1, 0.5)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Adds a role to all mentioned users.")]
    public Task Add(IRole role, params IGuildUser[] users) =>
      ForEvery(users, Do(async u => await u.AddRoleAsync(role)));

    [Command("list")]
    [ChannelRateLimit(1, 1)]
    [GuildRateLimit(1, 0.5)]
    [Remarks("Lists all roles on this server.")]
    public Task List() =>
      RespondAsync(Check.NotNull(Context.Guild).Roles
          .Where(r => r.Id != Context.Guild.EveryoneRole.Id)
          .OrderBy(r => r.Position)
          .Select(r => r.Name)
          .Join(", "));

    [Log]
    [Command("remove")]
    [GuildRateLimit(1, 0.5)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users.")]
    public Task Remove(IRole role, params IGuildUser[] users) =>
      ForEvery(users, Do(u => u.RemoveRoleAsync(role)));

    [Log]
    [Command("nuke")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes all roles from provided users.")]
    public Task Nuke(params SocketGuildUser[] users) =>
      ForEvery(users, Do(u => u.RemoveRolesAsync(u.GetRoles())));

    [Log]
    [Command("ban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Bans all mentioned users from a specified role.")]
    public async Task Ban(IRole role, params IGuildUser[] users) {
      await ForEvery(users, Do(async u => {
          await u.RemoveRoleAsync(role);
          var userRole = await Db.GetUserRole(u, role);
          userRole.IsBanned = true;
        }));
      await Db.Save();
    }

    [Log]
    [Command("unban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Unban all mentioned users from a specified role.")]
    public async Task Unban(IRole role, params IGuildUser[] users) {
      await ForEvery(users, Do(async u => {
          var userRole = await Db.GetUserRole(u, role);
          userRole.IsBanned = false;
        }));
      await Db.Save();
    }

    [Log]
    [Command("create")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Creates a mentionable role and applies it to all mentioned users")]
    public async Task Create(string name) {
      await Check.NotNull(Context.Guild).CreateRoleAsync(name);
      await Success();
    }

    [Log]
    [Command("delete")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Deletes a role and removes it from all users.")]
    public Task Delete(params IRole[] roles) =>
      ForEvery(roles, Do((IRole r) => r.DeleteAsync()));

    [Log]
    [Command("color")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Sets a role's color.")]
    public Task RoleColor(string color, params IRole[] roles) {
      uint colorVal;
      if(!TryParseColor(color, out colorVal)) {
        return RespondAsync($"Could not parse {color} to a proper color value");
      }
      return ForEvery(roles, Do(role =>
        role.ModifyAsync(r => {
          r.Color = new Optional<Color>(new Color(colorVal));
        })));
    }

    [Log]
    [Command("rename")]
    [GuildRateLimit(1, 1)]
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

