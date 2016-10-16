using System;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace Hourai {

[PublicOnly]
[ModuleCheck(ModuleType.Feeds)]
public partial class Feeds : HouraiModule {

  public Feeds() {
    const string JoinMsg = "$mention has joined the server.";
    const string LeaveMsg = "$mention has left the server.";
    const string BanMsg = "$mention has been banned.";
    Bot.Client.UserJoined += u => GuildMessage(c => c.JoinMessage, JoinMsg)(u, u.Guild);
    Bot.Client.UserLeft += u => GuildMessage(c => c.LeaveMessage, LeaveMsg)(u, u.Guild);
    Bot.Client.UserBanned += GuildMessage(c => c.BanMessage, BanMsg);
  }

  Func<IUser, IGuild, Task> GuildMessage(Func<Channel, bool> msg, string defaultMsg) {
    return async (u, g) => {
      var db = Bot.Database;
      var guildConfig = await db.GetGuild(g);
      foreach(var channel in guildConfig.Channels.ToArray()) {
        if(!msg(channel))
          continue;
        var dChannel = (await g.GetChannelAsync(channel.Id)) as ITextChannel;
        if(dChannel == null) {
          guildConfig.Channels.Remove(channel);
          db.Channels.Remove(channel);
          continue;
        }
        await dChannel.Respond(ProcessMessage(defaultMsg, u));
      }
    };
  }

  string ProcessMessage(string message, IUser user) {
    return message.Replace("$user", user.Username)
      .Replace("$mention", user.Mention);
  }

}

}
