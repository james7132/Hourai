using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;
using System;
using System.Threading.Tasks;

namespace Hourai.Feeds {

public partial class Feeds {

  [Group("announce")]
  public class Announce : DatabaseHouraiModule {

    public Announce(DatabaseService db) : base(db) {
    }

    [Command("join")]
    [Remarks("Enables or disables server join messages in the current channel")]
    public Task Join() => SetMessage(c => c.JoinMessage = !c.JoinMessage,
        c => c.JoinMessage,
        "Join");

    [Command("leave")]
    [Remarks("Enables or disables server leave messages in the current channel")]
    public Task Leave() => SetMessage(c => c.LeaveMessage = !c.LeaveMessage,
        c => c.LeaveMessage,
        "Leave");

    [Command("ban")]
    [Remarks("Enables or disables server ban messages in the current channel")]
    public Task Ban() => SetMessage(c => c.BanMessage = !c.BanMessage,
        c => c.BanMessage,
        "Ban");

    [Command("voice")]
    [Remarks("Enables or disables voice announcement messages in the current channel")]
    public Task Voice() => SetMessage(c => c.VoiceMessage = !c.VoiceMessage,
        c => c.VoiceMessage,
        "Voice");

    [Command("stream")]
    [Remarks("Enables or disables stream announcement messages in the current channel")]
    public Task Stream() => SetMessage(c => c.StreamMessage = !c.StreamMessage,
        c => c.StreamMessage,
        "Stream");

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
