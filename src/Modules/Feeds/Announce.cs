using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

public partial class Feeds {

  [Group("announce")]
  public class Announce : DatabaseHouraiModule {

    public Announce(DatabaseService db) : base(db) {
    }

    [Command("join")]
    [ChannelRateLimit(1, 1)]
    [Remarks("Enables or disables server join messages in the current channel")]
    [RequirePermission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Join() => SetMessage(c => c.JoinMessage = !c.JoinMessage,
        c => c.JoinMessage,
        "Join");

    [Command("leave")]
    [ChannelRateLimit(1, 1)]
    [Remarks("Enables or disables server leave messages in the current channel")]
    [RequirePermission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Leave() => SetMessage(c => c.LeaveMessage = !c.LeaveMessage,
        c => c.LeaveMessage,
        "Leave");

    [Command("ban")]
    [ChannelRateLimit(1, 1)]
    [Remarks("Enables or disables server ban messages in the current channel")]
    [RequirePermission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Ban() => SetMessage(c => c.BanMessage = !c.BanMessage,
        c => c.BanMessage,
        "Ban");

    [Command("voice")]
    [ChannelRateLimit(1, 1)]
    [Remarks("Enables or disables voice messages in the current channel")]
    [RequirePermission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public Task Voice() => SetMessage(c => c.VoiceMessage = !c.VoiceMessage,
        c => c.VoiceMessage,
        "Voice");

    static string Status(bool status) => status ? "enabled" : "disabled";

    async Task SetMessage(Action<Channel> alteration,
        Func<Channel, bool> val,
        string messageType) {
      var channel = DbContext.GetChannel(Context.Channel as ITextChannel);
      alteration(channel);
      await DbContext.Save();
      await Success($"{messageType} message {Status(val(channel))}");
    }

  }

}

}
