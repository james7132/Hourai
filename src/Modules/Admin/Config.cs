using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

public partial class Admin {

  [Group("config")]
  [RequireContext(ContextType.Guild)]
  public class ConfigGroup : DatabaseHouraiModule {

    public ConfigGroup(DatabaseService db) : base(db) {
    }

    [Log]
    [Command("prefix")]
    [GuildRateLimit(1, 60)]
    [Remarks("Sets the bot's command prefix for this server.")]
    [RequirePermission(GuildPermission.ManageGuild, Require.BotOwnerOverride)]
    public async Task Prefix([Remainder] string prefix) {
      if(string.IsNullOrEmpty(prefix)) {
        await RespondAsync("Cannot set bot prefix to an empty result");
        return;
      }
      var guild = DbContext.GetGuild(Context.Guild);
      guild.Prefix = prefix.Substring(0, 1);
      await DbContext.Save();
      await Success($"Bot command prefix set to {guild.Prefix.ToString().Code()}");
    }

  }

}

}
