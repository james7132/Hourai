using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Preconditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

public abstract class TempModule : HouraiModule {

  protected Task TempAction(string actionType,
      TimeSpan time,
      IEnumerable<IGuildUser> users,
      Func<IGuildUser, AbstractTempAction> action,
      Func<IGuildUser, AbstractTempAction, Task> postAction = null) =>
    TempAction(actionType, time, users, u => Task.FromResult(action(u)), postAction);

  protected async Task TempAction(string actionType,
      TimeSpan time,
      IEnumerable<IGuildUser> users,
      Func<IGuildUser, Task<AbstractTempAction>> action,
      Func<IGuildUser, AbstractTempAction, Task> postAction = null) {
    Check.NotNull(action);
    var expiration = DateTimeOffset.Now + time;
    await ForEvery(users, Do(async delegate(IGuildUser user) {
        var tempAction = await action(user);
        tempAction.User = await Db.GetGuildUser(user);
        tempAction.UserId = user.Id;
        tempAction.GuildId = Context.Guild.Id;
        tempAction.Expiration = expiration;
        Db.TempActions.Add(tempAction);
        if(postAction != null)
          await postAction(user, tempAction);
        if (tempAction.Reverse)
          await tempAction.Unapply(Context.Client);
        else
          await tempAction.Apply(Context.Client);
      }));
    await Db.Save();
  }

}

public partial class Admin {

  [Group("temp")]
  public class Temp : TempModule {

    [Log]
    [Command("ban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily bans user(s) from the server.")]
    public Task Ban(TimeSpan time, params IGuildUser[] users) {
      var guild = Check.NotNull(Context.Guild);
      Func<IGuildUser, AbstractTempAction, Task> postAction = async (user, tempAction) => {
        try {
          await user.SendDMAsync($"You have been temporarily banned from {guild.Name}. " +
             $"You will be unbanned at {tempAction.Expiration} UTC.");
        } catch(Exception e) {
          Log.Error(e);
        }
      };
      return TempAction("ban", time, users, u => new TempBan());
    }

    [Log]
    [Command("mute")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily mute user(s).")]
    public Task Mute(TimeSpan time, params IGuildUser[] users) =>
      TempAction("mute", time, users, u => new TempMute());

    [Log]
    [Command("unmute")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily unmute user(s).")]
    public Task Unmute(TimeSpan time, params IGuildUser[] users) =>
      TempAction("unmute", time, users, u => new TempMute { Reverse = true });

    [Log]
    [Command("deafen")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily deafens user(s).")]
    public Task Deafen(TimeSpan time, params IGuildUser[] users) =>
      TempAction("deafen", time, users, u => new TempDeafen());

    [Log]
    [Command("undeafen")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily undeafens user(s).")]
    public Task Undeafen(TimeSpan time, params IGuildUser[] users) =>
      TempAction("undeafen", time, users, u => new TempDeafen { Reverse = true });

    [Log]
    [Group("role")]
    [RequirePermission(GuildPermission.ManageRoles)]
    public class Roles : TempModule {

      [Command("add")]
      [GuildRateLimit(1, 1)]
      public async Task Add(TimeSpan time, IRole role, params IGuildUser[] users) {
        await Db.Roles.Get(role);
        await TempAction("add role", time, users, user => new TempRole {
           RoleId = role.Id
        });
      }

      [Command("remove")]
      [GuildRateLimit(1, 1)]
      public async Task Remove(TimeSpan time, IRole role, params IGuildUser[] users) {
        await Db.Roles.Get(role);
        await TempAction("remove role", time, users, user => new TempRole {
           RoleId = role.Id,
           Reverse = true
        });
      }

    }

  }

}

}

