using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("config")]
  public class ConfigGroup : HouraiModule {

    //BotDbContext Database { get; }

    //public ConfigGroup(BotDbContext db) {
      //Database = db;
    //}      

    //[Command("prefix")]
    //[PublicOnly, Permission(GuildPermission.ManageGuild)]
    //[Remarks("Sets the bot's command prefix for this server.")]
    //public async Task Prefix(string prefix) {
      //if(string.IsNullOrEmpty(prefix)) {
        //await RespondAsync("Cannot set bot prefix to an empty result");
        //return;
      //}
      //var guild = await Database.GetGuild(Check.InGuild(Context.Message).Guild);
      //guild.Prefix = prefix[0];
      //await Database.Save();
      //await Success($"Bot command prefix set to {guild.Prefix}");
    //}

  }

}

}
