using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public partial class Admin {

  [Group("prune")]
  public class PruneGroup {

    [Command]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
    public Task Prune(IUserMessage msg, int count = 100) => 
      PruneMessages(Check.InGuild(msg), m => true, count);

    [Command("user")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
    public Task PruneUser(IUserMessage msg, params IGuildUser[] users) {
      var userSet = new HashSet<IUser>(users);
      return PruneMessages(Check.InGuild(msg), m => userSet.Contains(m.Author));
    }

    [Command("embed")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages with embeds or attachments in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Embed(IUserMessage msg, int count = 100) =>
      PruneMessages(Check.InGuild(msg), m => m.Embeds.Any() || m.Attachments.Any(), count);

    [Command("mine")]
    [Remarks("Removes all messages from the user using the command in the last X messages.")]
    public Task Mine(IUserMessage msg, int count = 100) {
      ulong id = msg.Author.Id;
      return PruneMessages(Check.InGuild(msg), m => m.Author.Id == id, count);
    }

    [Command("ping")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages that mentioned other users or roles the last X messages. Requires ``Manage Messages`` permission.")]
    public Task Mention(IUserMessage msg, int count = 100) => 
      PruneMessages(Check.InGuild(msg), m => m.MentionedUsers.Any() || m.MentionedRoles.Any(), count);

    [Command("bot")]
    [Permission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all bots in the last X messages. Requires ``Manage Messages`` permission.")]
    public Task PruneBot(IUserMessage msg, int count = 100) =>
      PruneMessages(Check.InGuild(msg), m => m.Author.IsBot, count);

    static async Task PruneMessages(IMessageChannel channel,
        Func<IMessage, bool> pred = null,
        int count = 100) {
      if (count > Config.PruneLimit)
        count = Config.PruneLimit;
      if (count < 0) {
        await channel.Respond("Cannot prune a negative count of messages");
        return;
      }
      var finalCount = count;
      var messages = await channel.GetMessagesAsync(count);
      IEnumerable<IMessage> allMessages = messages;
      if (pred != null) {
        var filtered = messages.Where(pred).ToArray();
        finalCount = Math.Min(finalCount, filtered.Length);
        allMessages = filtered;
      }
      await channel.DeleteMessagesAsync(allMessages
          .OrderByDescending(m => m.Timestamp)
          .Take(count));
      await channel.Success($"Deleted {finalCount} messages.");
    }
  }
}

}

