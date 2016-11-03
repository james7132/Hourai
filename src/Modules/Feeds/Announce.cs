using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace Hourai {

public partial class Feeds {

  [Group("announce")]
  public class Announce : DatabaseHouraiModule {

    public Announce(BotDbContext db) : base(db) {
    }

    [Command("join")]
    [Permission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Join() => SetMessage(c => c.JoinMessage = !c.JoinMessage, "Join");

    [Command("leave")]
    [Permission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Leave() => SetMessage(c => c.LeaveMessage = !c.LeaveMessage, "Leave");

    [Command("ban")]
    [Permission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Ban() => SetMessage(c => c.BanMessage = !c.BanMessage, "Ban");

    [Command("voice")]
    [Permission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Voice() => SetMessage(c => c.VoiceMessage = !c.VoiceMessage, "Voice");

    static string Status(bool status) => status ? "enabled" : "disabled";

    async Task SetMessage(Action<Channel> alteration, string messageType) {
      var channel = Database.GetChannel(Context.Channel as ITextChannel);
      alteration(channel);
      await Database.Save();
      await Success($"{messageType} message {Status(channel.BanMessage)}");
    }

  }

}

}
