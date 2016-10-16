using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("prune")]
  public class Prune : HouraiModule {

    [Command]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
    public Task Prune(int count = 100) => 
      PruneMessages(m => true, count);

    [Command("user")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
    public Task PruneUser(params IGuildUser[] users) {
      var userSet = new HashSet<IUser>(users);
      return PruneMessages(m => userSet.Contains(m.Author));
    }

    [Command("embed")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages with embeds or attachments in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Embed(int count = 100) =>
      PruneMessages(m => m.Embeds.Any() || m.Attachments.Any(), count);

    [Command("mine")]
    [Remarks("Removes all messages from the user using the command in the last X messages.")]
    public Task Mine(int count = 100) {
      ulong id = Context.Message.Author.Id;
      return PruneMessages(m => m.Author.Id == id, count);
    }

    [Command("ping")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages that mentioned other users or roles the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Mention(int count = 100) => 
      PruneMessages(m => m.MentionedUserIds.Any() || m.MentionedRoleIds.Any(), count);

    [Command("bot")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all bots in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task PruneBot(int count = 100) =>
      PruneMessages(m => m.Author.IsBot, count);

    async Task PruneMessages(Func<IMessage, bool> pred = null,
        int count = 100) {
      var channel = Context.Channel;
      if (count > Config.PruneLimit)
        count = Config.PruneLimit;
      if (count < 0) {
        await channel.Respond("Cannot prune a negative count of messages");
        return;
      }
      var actualCount = 0;
      await channel.GetMessagesAsync(count).ForEachAsync(async m => {
          IEnumerable<IMessage> messages = m;
          if (pred != null)
            messages = m.Where(pred);
          actualCount += m.Count();
          await channel.DeleteMessagesAsync(m);
        });
      await Success($"Deleted {actualCount} messages.");
    }
  }
}

}

