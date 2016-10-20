using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai {

public partial class Feeds { 

  [Group("reddit")]
  public class Reddit : HouraiModule {

    [Command("add")]
    [Alias("+")]
    [Permission(GuildPermission.ManageGuild)]
    public async Task Add(string subreddit) {
    }

    [Command("remove")]
    [Alias("-")]
    [Permission(GuildPermission.ManageGuild)]
    public async Task Remove(string subreddit) {
    }

    [Command("list")]
    [Permission(GuildPermission.ManageGuild)]
    public async Task List() {
    }

  }
}


}
