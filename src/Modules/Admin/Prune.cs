using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

public static class AsyncEnumerable {

  public static Task ForEachAwait<T>(
      this IAsyncEnumerable<T> enumerable,
      Func<T, Task> action) {
    return ForEachAwait(enumerable, action, CancellationToken.None);
  }

  public static async Task ForEachAwait<T>(
      this IAsyncEnumerable<T> enumerable,
      Func<T, Task> action,
      CancellationToken cancellationToken) {
    using (var enumerator = enumerable.GetEnumerator()) {
      if (await enumerator.MoveNext(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) {
        Task<bool> moveNextTask;
        do {
          var current = enumerator.Current;
          moveNextTask = enumerator.MoveNext(cancellationToken);
          await action(current); //now with await
        } while (await moveNextTask.ConfigureAwait(continueOnCapturedContext: false));
      }
    }
  }

}

public partial class Admin {

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
      ulong id = Context.Message.Author.Id;
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
      await Context.Channel.GetMessagesAsync(count).ForEachAwait(async m => { IEnumerable<IUserMessage> messages = m.OfType<IUserMessage>()
              .Where(message => message.Reactions.Count > 0);
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
          actualCount += messages.Count();
          await channel.DeleteMessagesAsync(messages);
        });
      await Success($"Deleted {actualCount} messages.");
    }
  }
}

}

