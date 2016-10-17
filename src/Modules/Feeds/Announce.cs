using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai {

public partial class Feeds {

  [Group("announce")]
  public class Announce : DatabaseHouraiModule {

    public Announce(BotDbContext db) : base(db) {
    }

    static string Status(bool status) {
      return status ? "enabled" : "disabled";
    }

    [Command("join")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Join() {
      var channel = await Database.GetChannel(Context.Channel as ITextChannel);
      channel.JoinMessage = !channel.JoinMessage;
      await Database.Save();
      await Success($"Join message {Status(channel.JoinMessage)}");
    }

    [Command("leave")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Leave() {
      var channel = await Database.GetChannel(Context.Channel as ITextChannel);
      channel.LeaveMessage = !channel.LeaveMessage;
      await Database.Save();
      await Success($"Leave message {Status(channel.LeaveMessage)}");
    }

    [Command("ban")]
    [Permission(GuildPermission.ManageGuild, Require.User)]
    public async Task Ban() {
      var channel = await Database.GetChannel(Context.Channel as ITextChannel);
      channel.BanMessage = !channel.BanMessage;
      await Database.Save();
      await Success($"Ban message {Status(channel.BanMessage)}");
    }

  }

}

}
