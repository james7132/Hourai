using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class AnnounceService {

  BotDbContext Database { get; }
  DiscordSocketClient Client { get; }

  public AnnounceService(IDependencyMap map) {
    Database = map.Get<BotDbContext>();
    Client = map.Get<DiscordSocketClient>();
    const string JoinMsg = "$mention has joined the server.";
    const string LeaveMsg = "$mention has left the server.";
    const string BanMsg = "$mention has been banned.";
    Client.UserJoined += u => GuildMessage(c => c.JoinMessage, JoinMsg)(u, u.Guild);
    Client.UserLeft += u => GuildMessage(c => c.LeaveMessage, LeaveMsg)(u, u.Guild);
    Client.UserBanned += GuildMessage(c => c.BanMessage, BanMsg);
  }

  string ProcessMessage(string message, IUser user) {
    return message.Replace("$user", user.Username)
      .Replace("$mention", user.Mention);
  }

  Func<IUser, IGuild, Task> GuildMessage(Func<Channel, bool> msg, string defaultMsg) {
    return async (u, g) => {
      var guildConfig = Database.GetGuild(g);
      foreach(var channel in guildConfig.Channels.ToArray()) {
        if(!msg(channel))
          continue;
        var dChannel = (await g.GetChannelAsync(channel.Id)) as ITextChannel;
        if(dChannel == null) {
          guildConfig.Channels.Remove(channel);
          Database.Channels.Remove(channel);
          continue;
        }
        await dChannel.Respond(ProcessMessage(defaultMsg, u));
      }
    };
  }


}


}
