using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;
using System.Threading.Tasks;

namespace Hourai.Admin {

public partial class Admin {

  [Group("config")]
  [RequireContext(ContextType.Guild)]
  public class ConfigGroup : HouraiModule {

    [Log]
    [Command("prefix")]
    [GuildRateLimit(1, 60)]
    [Remarks("Sets the bot's command prefix for this server.")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    public async Task Prefix([Remainder] string prefix) {
      if(string.IsNullOrEmpty(prefix)) {
        await RespondAsync("Cannot set bot prefix to an empty result");
        return;
      }
      var guild = Context.DbGuild;
      guild.Prefix = prefix.Substring(0, 1);
      await Db.Save();
      await Success($"Bot command prefix set to {guild.Prefix.ToString().Code()}");
    }

  }

}

}
