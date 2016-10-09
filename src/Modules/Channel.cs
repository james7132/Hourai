using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("channel")]
  public class ChannelGroup {

    [Command("create")]
    [Permission(GuildPermission.ManageChannels, Require.Both)]
    [Remarks("Creates a public channel with a specified name. Requires ``Manage Channels`` permission.")]
    public async Task Create(IUserMessage msg, string name) {
      var guild = Check.InGuild(msg).Guild;
      var channel = await guild.CreateTextChannelAsync(name); 
      await msg.Success($"{channel.Mention} created.");
    }

    [Command("delete")]
    [Permission(GuildPermission.ManageChannels, Require.Both)]
    [Remarks("Deletes all mentioned channels. Requires ``Manage Channels`` permission.")]
    public Task Delete(IUserMessage msg, params IGuildChannel[] channels) {
      return CommandUtility.ForEvery(msg, channels, CommandUtility.Action(
            delegate(IGuildChannel channel) {
              return channel.DeleteAsync();
            }));
    }

    [Command("list")]
    [Remarks("Responds with a list of all text channels that the bot can see on this server.")]
    public async Task List(IUserMessage msg) {
      var guild = Check.InGuild(msg).Guild;
      var channels = (await guild.GetChannelsAsync()).OfType<ITextChannel>();
      await msg.Respond(channels.OrderBy(c => c.Position)
          .Select(c => c.Mention).Join(", "));
    }

    [Command("permissions")]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IUserMessage msg, IGuildUser user = null) {
      user = user ?? (msg.Author as IGuildUser);
      var perms = user.GetPermissions(Check.InGuild(msg));
      await msg.Respond(perms.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

  }

}

}
