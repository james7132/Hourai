using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class AnnounceService {

  DiscordSocketClient Client { get; }

  public AnnounceService(IDependencyMap map) {
    Client = map.Get<DiscordSocketClient>();
    //TODO(james7132): make these messages configurable
    const string JoinMsg = "$mention has joined the server.";
    const string LeaveMsg = "**$user** has left the server.";
    const string BanMsg = "**$user** has been banned.";
    Client.UserJoined += u => GuildMessage(c => c.JoinMessage, JoinMsg)(u, u.Guild);
    Client.UserLeft += u => GuildMessage(c => c.LeaveMessage, LeaveMsg)(u, u.Guild);
    Client.UserBanned += GuildMessage(c => c.BanMessage, BanMsg);
    Client.UserVoiceStateUpdated += VoiceStateChanged;
  }

  string GetUserString(IUser user) {
    string nickname = (user as IGuildUser)?.Nickname;
    if(string.IsNullOrEmpty(nickname))
      return user.Username;
    return nickname;
  }

  async Task VoiceStateChanged(SocketUser user,
      SocketVoiceState before,
      SocketVoiceState after) {
    var guild = (user as IGuildUser)?.Guild;
    if(guild == null)
      return;
    using (var context = new BotDbContext()) {
      var guildConfig = context.GetGuild(guild);
      string changes = null;
      var userString = GetUserString(user).Bold();
      if(before.VoiceChannel?.Id != after.VoiceChannel?.Id) { if(after.VoiceChannel != null) {
          changes = userString + " joined " + after.VoiceChannel?.Name.Bold();
        } else {
          changes = userString + " left " + before.VoiceChannel?.Name.Bold();
        }
      }

      if(string.IsNullOrEmpty(changes))
        return;

      foreach(var channel in guildConfig.Channels) {
        if(!channel.VoiceMessage)
          continue;
        var dChannel = (await guild.GetChannelAsync(channel.Id)) as ITextChannel;
        if(dChannel == null) {
          guildConfig.Channels.Remove(channel);
          context.Channels.Remove(channel);
          continue;
        }
        await dChannel.Respond(changes);
      }
    }
  }

  string ProcessMessage(string message, IUser user) {
    return message.Replace("$user", user.Username)
      .Replace("$mention", user.Mention);
  }

  Func<IUser, IGuild, Task> GuildMessage(Func<Channel, bool> msg, string defaultMsg) {
    return async (u, g) => {
      using (var context = new BotDbContext()) {
        var guildConfig = context.GetGuild(g);
        foreach(var channel in guildConfig.Channels) {
          if(!msg(channel))
            continue;
          var dChannel = (await g.GetChannelAsync(channel.Id)) as ITextChannel;
          if(dChannel == null) {
            guildConfig.Channels.Remove(channel);
            context.Channels.Remove(channel);
            continue;
          }
          await dChannel.Respond(ProcessMessage(defaultMsg, u));
        }
      }
    };
  }


}


}
