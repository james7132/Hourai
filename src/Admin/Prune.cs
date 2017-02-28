using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hourai.Extensions;
using Hourai.Preconditions;

namespace Hourai.Admin {

public partial class Admin {

  [Log]
  [Group("prune")]
  public class Prune : HouraiModule {

    [Command]
    [ChannelRateLimit(5, 30)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes the last X messages from the current channel.")]
    public Task Default(int count = 100) => PruneMessages(m => true, count);

    [Command("user")]
    [ChannelRateLimit(5, 30)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all mentioned users in the last 100 messages.")]
    public Task User(params IGuildUser[] users) {
      var userSet = new HashSet<IUser>(users);
      return PruneMessages(m => userSet.Contains(m.Author));
    }

    [Command("embed")]
    [ChannelRateLimit(5, 30)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages with embeds or attachments in the last X messages.")]
    public Task Embed(int count = 100) =>
      PruneMessages(m => m.Embeds.Any() || m.Attachments.Any(), count);

    [Command("mine")]
    [UserRateLimit(1, 5)]
    [Remarks("Removes all messages from the user using the command in the last X messages.")]
    public Task Mine(int count = 100) {
      ulong id = Context.User.Id;
      return PruneMessages(m => m.Author.Id == id, count);
    }

    [Command("ping")]
    [ChannelRateLimit(5, 30)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages that mentioned other users or roles the last X messages.")]
    public Task Mention(int count = 100) =>
      PruneMessages(m => m.MentionedUserIds.Any() || m.MentionedRoleIds.Any(), count);

    [Command("bot")]
    [ChannelRateLimit(5, 30)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes all messages from all bots in the last X messages.")]
    public Task Bot(int count = 100) =>
      PruneMessages(m => m.Author.IsBot, count);

    [Command("reaction")]
    [ChannelRateLimit(1, 15)]
    [RequirePermission(GuildPermission.AddReactions)]
    [RequirePermission(GuildPermission.ManageMessages)]
    [Remarks("Removes all reactions from messages in the last X messages.")]
    public async Task Reactions(int count = 100) {
      await Context.Channel.GetMessagesAsync(count).ForEachAwait(async msg => {
          var messages = msg.OfType<IUserMessage>().Where(m => m.Reactions.Any());
          foreach (IUserMessage message in messages) {
            await message.RemoveAllReactionsAsync();
            await Task.Delay(2000);
          }
        });
      await Success();
    }

    async Task PruneMessages(Func<IMessage, bool> pred = null,
        int count = 100) {
      var channel = Context.Channel;
      if (count < 0) {
        await channel.Respond("Cannot prune a negative count of messages");
        return;
      }
      var actualCount = 0;
      await channel.GetMessagesAsync(count).ForEachAwait(async m => {
          IEnumerable<IMessage> messages = m;
          if (pred != null)
            messages = m.Where(pred);
          actualCount += messages.Count();
          await channel.DeleteMessagesAsync(messages);
        });
      await Success($"Deleted {actualCount} messages.");
    }
  }
}

}

