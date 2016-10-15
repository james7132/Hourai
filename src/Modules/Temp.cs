using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("temp")]
  public class TempGroup {

    public TempGroup() {
      Log.Info("INIT TEMP GROUP");
      Bot.RegularTasks += CheckTempActions;
    }

    async Task CheckTempActions() {
      Log.Info("CHECKING TEMP ACTIONS");
      var actions = Bot.Database.TempActions.OrderByDescending(b => b.End);
      var now = DateTimeOffset.Now;
      var done = new List<AbstractTempAction>();
      foreach(var action in actions) {
        Log.Info($"({action.GuildId}, {action.Id}): {action.Start}, {action.End}, {action.End - now}");
        if(action.End >= now)
          break;
        await action.Unapply(Bot.Client);
        done.Add(action);
      }
      if(done.Count > 0) {
        Bot.Database.TempActions.RemoveRange(done);
        await Bot.Database.Save();
      }
    }

    static async Task TempAction(IUserMessage msg, 
        TimeSpan time, 
        Func<IGuildUser, Task<AbstractTempAction>> action, 
        IEnumerable<IGuildUser> users,
        Func<IGuildUser, AbstractTempAction, Task> postAction = null) {
      Check.NotNull(action);
      var start = DateTimeOffset.Now;
      var end = DateTimeOffset.Now + time;
      var commandAction = await CommandUtility.Action(msg, "temp ban",
          async delegate(IGuildUser user) {
          var tempAction = await action(user);
          tempAction.Start = start;
          tempAction.End = end;
          Bot.Database.TempActions.Add(tempAction);
          await Bot.Database.Save();
          if(postAction != null)
            await postAction(user, tempAction);
          await tempAction.Apply(Bot.Client);
          });
      await CommandUtility.ForEvery(msg, users, commandAction);
    }

    [Command("ban")]
    [Permission(GuildPermission.BanMembers)]
    [Remarks("Temporarily bans user(s) from the server. Requires ``Ban Members`` server permission")]
    public Task Ban(IUserMessage msg, string time, params IGuildUser[] users) {
      var guild = Check.InGuild(msg).Guild;
      TimeSpan timespan;
      if(!TimeSpan.TryParse(time, out timespan)) {
        return msg.Respond($"Could not convert \"{time}\" into a valid timespan. See https://msdn.microsoft.com/en-us/library/se73z7b9(v=vs.110).aspx#Anchor_2 for more details");
      }
      Func<IGuildUser, Task<AbstractTempAction>> action = async user => new TempBan {
         UserId = user.Id,
         GuildId = user.Guild.Id,
         User = await Bot.Database.GetUser(user),
         Guild = await Bot.Database.GetGuild(user.Guild),
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
      return TempAction(msg, timespan, action, users, postAction);
    }
  
    [Group("role")]
    public class RoleGroup {

      [Command("add")]
      [Permission(GuildPermission.ManageRoles)]
      public Task Add(IUserMessage msg, IRole role, string time, params IGuildUser[] users) {
        var guild = Check.InGuild(msg).Guild;
        TimeSpan timespan;
        if(!TimeSpan.TryParse(time, out timespan)) {
          return msg.Respond($"Could not convert \"{time}\" into a valid timespan. See https://msdn.microsoft.com/en-us/library/se73z7b9(v=vs.110).aspx#Anchor_2 for more details");
        }
        Func<IGuildUser, Task<AbstractTempAction>> action = async user => new TempRole {
           UserId = user.Id,
           GuildId = user.Guild.Id,
           User = await Bot.Database.GetUser(user),
           Guild = await Bot.Database.GetGuild(user.Guild),
           RoleId = role.Id,
        };
        return TempAction(msg, timespan, action, users);
      }

    }

  }

}

}

