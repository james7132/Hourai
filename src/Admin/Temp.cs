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

public abstract class TempModule : DatabaseHouraiModule {

  protected DiscordShardedClient Client { get; }

  public TempModule(DiscordShardedClient client, DatabaseService db) : base(db) {
    Client = client;
  }

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
    var start = DateTimeOffset.Now;
    var end = start + time;
    await ForEvery(users, Do(async delegate(IGuildUser user) {
        var tempAction = await action(user);
        tempAction.Start = start;
        tempAction.End = end;
        DbContext.TempActions.Add(tempAction);
        await DbContext.Save();
        if(postAction != null)
          await postAction(user, tempAction);
        await tempAction.Apply(Client);
      }));
  }

}

public partial class Admin {

  [Group("temp")]
  public class Temp : TempModule {

    public Temp(DiscordShardedClient client, DatabaseService db) : base(client, db) {
    }

    [Log]
    [Command("ban")]
    [GuildRateLimit(1, 1)]
    [RequirePermission(GuildPermission.BanMembers)]
    [Remarks("Temporarily bans user(s) from the server.")]
    public Task Ban(TimeSpan time, params IGuildUser[] users) {
      var guild = Check.NotNull(Context.Guild);
      Func<IGuildUser, AbstractTempAction> action = user => new TempBan {
         UserId = user.Id,
         GuildId = user.Guild.Id,
         User = Context.Author,
         Guild = Context.DbGuild
      };
      Func<IGuildUser, AbstractTempAction, Task> postAction = async (user, tempAction) => {
        try {
          await user.SendDMAsync($"You have been temporarily banned from {guild.Name}. " +
             $"You will be unbanned at {tempAction.End} UTC.");
        } catch(Exception e) {
          Log.Error(e);
        }
      };
      return TempAction("ban", time, users, action, postAction);
    }

    [Log]
    [Group("role")]
    [RequirePermission(GuildPermission.ManageRoles)]
    public class Roles : TempModule {

      public Roles(DiscordShardedClient client, DatabaseService db) : base(client, db) {
      }

      [Command("add")]
      [GuildRateLimit(1, 1)]
      public Task Add(IRole role, TimeSpan time, params IGuildUser[] users) {
        var guild = Check.NotNull(Context.Guild);
        return TempAction("add role", time, users, user => new TempRole {
           UserId = user.Id,
           GuildId = user.Guild.Id,
           User = Context.Author,
           Guild = Context.DbGuild,
           RoleId = role.Id,
        });
      }

    }

  }

}

}

