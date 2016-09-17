using System;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace DrumBot {

[Module]
[PublicOnly]
[ModuleCheck(ModuleType.Feeds)]
public class Feeds {

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

  [Group("announce")]
  public class Announce {

    static string Status(bool status) {
      return status ? "enabled" : "disabled";
    }

    [Command("join")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Join(IUserMessage msg) {
      var channel = await Bot.Database.GetChannel(msg.Channel as ITextChannel);
      channel.JoinMessage = !channel.JoinMessage;
      await Bot.Database.Save();
      await msg.Success($"Join message {Status(channel.JoinMessage)}");
    }

    [Command("leave")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Leave(IUserMessage msg) {
      var channel = await Bot.Database.GetChannel(msg.Channel as ITextChannel);
      channel.LeaveMessage = !channel.LeaveMessage;
      await Bot.Database.Save();
      await msg.Success($"Leave message {Status(channel.LeaveMessage)}");
    }

    [Command("ban")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Ban(IUserMessage msg) {
      var channel = await Bot.Database.GetChannel(msg.Channel as ITextChannel);
      channel.BanMessage = !channel.BanMessage;
      await Bot.Database.Save();
      await msg.Success($"Ban message {Status(channel.BanMessage)}");
    }

  }

}

}
