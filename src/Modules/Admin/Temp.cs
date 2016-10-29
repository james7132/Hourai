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

  protected Task TempAction(TimeSpan time, 
      Func<IGuildUser, AbstractTempAction> action, 
      IEnumerable<IGuildUser> users,
      Func<IGuildUser, AbstractTempAction, Task> postAction = null) =>
    TempAction(time, u => Task.FromResult(action(u)), users, postAction);

  protected async Task TempAction(TimeSpan time, 
      Func<IGuildUser, Task<AbstractTempAction>> action, 
      IEnumerable<IGuildUser> users,
      Func<IGuildUser, AbstractTempAction, Task> postAction = null) {
    Check.NotNull(action);
    var start = DateTimeOffset.Now;
    var end = DateTimeOffset.Now + time;
    var commandAction = await CommandUtility.Action(Context, "temp ban",
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
    public Task Ban(string time, params IGuildUser[] users) {
      var guild = Check.NotNull(Context.Guild);
      TimeSpan timespan;
      if(!TimeSpan.TryParse(time, out timespan)) {
        return Context.Channel.Respond($"Could not convert \"{time}\" into a valid timespan. See https://msdn.microsoft.com/en-us/library/se73z7b9(v=vs.110).aspx#Anchor_2 for more details");
      }
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
      return TempAction(timespan, action, users, postAction);
    }
  
    [Group("role")]
    public class Roles : TempModule {

      public Roles(DiscordSocketClient client, BotDbContext db) : base(client, db) {
      }

      [Command("add")]
      [Permission(GuildPermission.ManageRoles)]
      public Task Add(IRole role, string time, params IGuildUser[] users) {
        var guild = Check.NotNull(Context.Guild);
        TimeSpan timespan;
        if(!TimeSpan.TryParse(time, out timespan)) {
          return Context.Channel.Respond($"Could not convert \"{time}\" into a valid timespan. See https://msdn.microsoft.com/en-us/library/se73z7b9(v=vs.110).aspx#Anchor_2 for more details");
        }
        Func<IGuildUser, AbstractTempAction> action = user => new TempRole {
           UserId = user.Id,
           GuildId = user.Guild.Id,
           User = Database.GetUser(user),
           Guild = Database.GetGuild(user.Guild),
           RoleId = role.Id,
        };
        return TempAction(timespan, action, users);
      }

    }

  }

}

}

