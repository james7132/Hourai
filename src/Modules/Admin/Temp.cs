using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public abstract class TempModule : DatabaseHouraiModule {

  protected DiscordSocketClient Client { get; }

  public TempModule(DiscordSocketClient client, BotDbContext db) : base(db) {
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
    var end = DateTimeOffset.Now + time;
    var commandAction = await CommandUtility.Action(Context, "temp " + actionType,
      async delegate(IGuildUser user) {
        var tempAction = await action(user);
        tempAction.Start = start;
        tempAction.End = end;
        Database.TempActions.Add(tempAction);
        await Database.Save();
        if(postAction != null)
          await postAction(user, tempAction);
        await tempAction.Apply(Client);
      });
    await CommandUtility.ForEvery(Context, users, commandAction);
  }

}

public partial class Admin : HouraiModule {

  [Group("temp")]
  public class Temp : TempModule {

    public Temp(DiscordSocketClient client, BotDbContext db) : base(client, db) {
    }

    [Command("ban")]
    [Permission(GuildPermission.BanMembers)]
    [Remarks("Temporarily bans user(s) from the server. Requires ``Ban Members`` server permission")]
    public Task Ban(TimeSpan time, params IGuildUser[] users) {
      var guild = Check.NotNull(Context.Guild);
      Func<IGuildUser, AbstractTempAction> action = user => new TempBan {
         UserId = user.Id,
         GuildId = user.Guild.Id,
         User = Database.GetUser(user),
         Guild = Database.GetGuild(user.Guild),
      };
      Func<IGuildUser, AbstractTempAction, Task> postAction = async (user, tempAction) => {
        try {
          var dmChannel = await user.CreateDMChannelAsync();
          await dmChannel.Respond
            ($"You have been temporarily banned from {guild.Name}. " +
             $"You will be unbanned at {tempAction.End} UTC.");
        } catch(Exception e) {
          Log.Error(e);
        }
      };
      return TempAction("ban", time, users, action, postAction);
    }

    [Group("role")]
    [Permission(GuildPermission.ManageRoles)]
    public class Roles : TempModule {

      public Roles(DiscordSocketClient client, BotDbContext db) : base(client, db) {
      }

      [Command("add")]
      public Task Add(IRole role, TimeSpan time, params IGuildUser[] users) {
        var guild = Check.NotNull(Context.Guild);
        return TempAction("add role", time, users, user => new TempRole {
           UserId = user.Id,
           GuildId = user.Guild.Id,
           User = Database.GetUser(user),
           Guild = Database.GetGuild(user.Guild),
           RoleId = role.Id,
        });
      }

    }

  }

}

}

