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

    async Task SetSelfServe(IRole[] roles, bool val) {
      var dbRoles = await Task.WhenAll(roles.Select(r => Db.Roles.Get(r)));
      foreach(var role in dbRoles)
        role.SelfServe = val;
      await Db.Save();
      await Success();
    }

    [Log]
    [Command("allow")]
    [RequirePermission(GuildPermission.ManageRoles, Require.Bot)]
    [RequirePermission(new[] {GuildPermission.ManageGuild, GuildPermission.ManageRoles}, Require.BotOwnerOverride)]
    [Remarks("Enables self-serve for all provided roles.")]
    public Task Allow(params IRole[] roles) => SetSelfServe(roles, true);

    [Log]
    [Command("forbid")]
    [RequirePermission(GuildPermission.ManageRoles, Require.Bot)]
    [RequirePermission(new[] {GuildPermission.ManageGuild, GuildPermission.ManageRoles}, Require.BotOwnerOverride)]
    [Remarks("Disables self-serve for all provided roles.")]
    public Task Forbid(params IRole[] roles) => SetSelfServe(roles, false);

    [Log]
    [Command("get")]
    [RequirePermission(GuildPermission.ManageRoles, Require.Bot)]
    [Remarks("Gives the user a self-serve role. The role must be enabled via `role allow` first.")]
    public async Task Get(IRole role) {
      var dbRole = await Db.Roles.Get(role);
      if (dbRole.SelfServe) {
        var user = Context.Guild.GetUser(Context.User.Id);
        await user.AddRolesAsync(new[] {role});
        await Success();
      } else {
        await RespondAsync($"The role {role.Name} is not set up for self-serve.");
      }
    }

    [Log]
    [Command("drop")]
    [RequirePermission(GuildPermission.ManageRoles, Require.Bot)]
    [Remarks("Removes a self-serve role from the user. The role must be enabled via `role allow` first.")]
    public async Task Drop(IRole role) {
      var dbRole = await Db.Roles.Get(role);
      if (dbRole.SelfServe) {
        var user = Context.Guild.GetUser(Context.User.Id);
        await user.RemoveRolesAsync(new[] {role});
        await Success();
      } else {
        await RespondAsync($"The role {role.Name} is not set up for self-serve.");
      }
    }

    [Log]
    [Command("remove")]
    [GuildRateLimit(1, 0.5)]
    [RequirePermission(GuildPermission.ManageRoles)]
    [Remarks("Removes a role to all mentioned users.")]
    public Task Remove(IRole role, params IGuildUser[] users) =>
      ForEvery(users, Do(u => u.RemoveRoleAsync(role)));

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
    public Task Color(string color, params IRole[] roles) {
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

