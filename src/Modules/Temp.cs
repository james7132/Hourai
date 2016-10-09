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

    [Command("ban")]
    [Permission(GuildPermission.BanMembers)]
    [Remarks("Temporarily bans user(s) from the server. Requires ``Ban Members`` server permission")]
    public async Task Ban(IUserMessage msg, string time, params IGuildUser[] users) {
      var guild = Check.InGuild(msg).Guild;
      TimeSpan timespan;
      if(!TimeSpan.TryParse(time, out timespan)) {
        await msg.Respond($"Could not convert \"{time}\" into a valid timespan. See https://msdn.microsoft.com/en-us/library/se73z7b9(v=vs.110).aspx#Anchor_2 for more details");
        return;
      }
      var start = DateTimeOffset.Now;
      var end = DateTimeOffset.Now + timespan;
      var action = await CommandUtility.Action(msg, "temp ban",
          async delegate(IGuildUser user) {
            var tempBan = new TempBan {
              Id = user.Id,
              GuildId = user.Guild.Id,
              User = await Bot.Database.GetUser(user),
              Guild = await Bot.Database.GetGuild(user.Guild),
              Start = start,
              End = end
            };
            Bot.Database.TempBans.Add(tempBan);
            await Bot.Database.Save();
            try {
              var dmChannel = await user.CreateDMChannelAsync();
              await dmChannel.Respond($"You have been temporarily banned from {guild.Name}. You will be unbanned at {end} UTC.");
            } catch(Exception e) {
              Log.Error(e);
            }
            await user.BanAsync();
          });
      await CommandUtility.ForEvery(msg, users, action);
    }

  }

}

}

