using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

public partial class Admin {

  [Group("channel")]
  public class Channel : HouraiModule {

    [Command("create")]
    [RequirePermission(GuildPermission.ManageChannels)]
    [Remarks("Creates a public channel with a specified name. ")]
    public async Task Create(string name) {
      var channel = await Check.NotNull(Context.Guild).CreateTextChannelAsync(name);
      await Success($"{channel.Mention} created.");
    }

    [Command("delete")]
    [RequirePermission(GuildPermission.ManageChannels)]
    [Remarks("Deletes all mentioned channels.")]
    public Task Delete(params IGuildChannel[] channels) {
      return CommandUtility.ForEvery(Context, channels, CommandUtility.Action(
            delegate(IGuildChannel channel) {
              return channel.DeleteAsync();
            }));
    }

    [Command("list")]
    [Remarks("Responds with a list of all text channels that the bot can see on this server.")]
    public async Task List() {
      var channels = await Check.NotNull(Context.Guild).GetTextChannelsAsync();
      await RespondAsync(channels.OrderBy(c => c.Position).Select(c => c.Mention).Join(", "));
    }

    [Command("permissions")]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IGuildUser user = null) {
      user = user ?? (Context.User as IGuildUser);
      var perms = user.GetPermissions(Check.InGuild(Context.Message));
      await Context.Message.Respond(perms.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

  }

}

}
