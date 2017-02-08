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

    public Roles(DatabaseService db) : base(db) {
    }

    [Command("add")]
    [GuildRateLimit(1, 0.5)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Adds a role to all mentioned users.")]
    public Task Add(IRole role, params IGuildUser[] users) =>
      ForEvery(users, Do(async u => await u.AddRolesAsync(role)));

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

    [Command("remove")]
    [GuildRateLimit(1, 0.5)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users.")]
    public Task Remove(IRole role, params IGuildUser[] users) =>
      ForEvery(users, Do(u => u.RemoveRolesAsync(role)));

    [Command("nuke")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes all roles from provided users.")]
    public Task Nuke(params IGuildUser[] users) =>
      ForEvery(users, Do(u => u.RemoveRolesAsync()));

    [Command("ban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Bans all mentioned users from a specified role.")]
    public async Task Ban(IRole role, params IGuildUser[] users) {
      await ForEvery(users, Do(async u => {
          await u.RemoveRolesAsync(role);
          var guildUser = DbContext.GetGuildUser(u);
          guildUser.BanRole(role);
        }));
      await DbContext.Save();
    }

    [Command("unban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Unban all mentioned users from a specified role.")]
    public async Task Unban(IRole role, params IGuildUser[] users) {
      await ForEvery(users, Do(u => {
          var guildUser = DbContext.GetGuildUser(u);
          guildUser.UnbanRole(role);
          return Task.CompletedTask;
        }));
      await DbContext.Save();
    }

    [Command("create")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Creates a mentionable role and applies it to all mentioned users")]
    public async Task Create(string name) {
      await Check.NotNull(Context.Guild).CreateRoleAsync(name);
      await Success();
    }

    [Command("delete")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Deletes a role and removes it from all users.")]
    public Task Delete(params IRole[] roles) =>
      ForEvery(roles, Do((IRole r) => r.DeleteAsync()));

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

