using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;
using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Hourai.Feeds {

public partial class Feeds {

  [Group("announce")]
  [RequireContext(ContextType.Guild)]
  [RequirePermission(GuildPermission.SendMessages, Require.Bot)]
  public class Announce : HouraiModule {

    [Command("join")]
    [Remarks("Enables or disables server join messages in the current channel")]
    public Task Join() => SetMessage(c => c.JoinMessage, "Join");

    [Command("leave")]
    [Remarks("Enables or disables server leave messages in the current channel")]
    public Task Leave() => SetMessage(c => c.LeaveMessage, "Leave");

    [Command("ban")]
    [Remarks("Enables or disables server ban messages in the current channel")]
    public Task Ban() => SetMessage(c => c.BanMessage, "Ban");

    [Command("voice")]
    [Remarks("Enables or disables voice announcement messages in the current channel")]
    public Task Voice() => SetMessage(c => c.VoiceMessage, "Voice");

    [Command("stream")]
    [Remarks("Enables or disables stream announcement messages in the current channel")]
    public Task Stream() => SetMessage(c => c.StreamMessage, "Stream");

    static string Status(bool status) => status ? "enabled" : "disabled";

    async Task SetMessage(Expression<Func<Channel, bool>> alteration,
        string messageType) {
      var exp = (MemberExpression)alteration.Body;
      var prop = exp.Member as PropertyInfo;
      var channel = await Db.Channels.Get(Context.Channel as ITextChannel);
      var value = !((bool)prop.GetValue(channel));
      prop.SetValue(channel, value);
      await Db.Save();
      await Success($"{messageType} message {Status(value)}");
    }

  }

}

}
