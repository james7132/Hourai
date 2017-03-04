using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hourai {

public class CounterService : IService {

  public CounterSet Counters { get; set; }

  static readonly Regex UrlRegex = new Regex(@"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*$",
                                          RegexOptions.Compiled |
                                          RegexOptions.IgnoreCase);
  static readonly Regex PunctuationRegex = new Regex(@"[^a-zA-Z\d\s']",
                                              RegexOptions.Compiled);

  Task Increment(string key) {
      Counters.Get(key).Increment();
      return Task.CompletedTask;
  }

  public CounterService(DiscordShardedClient client) {
    client.MessageReceived += m => {
        var um = m as IUserMessage;
        if (um == null || m.Author.IsMe())
            return Task.CompletedTask;
        var text =  um.Resolve(TagHandling.Remove,
                            TagHandling.Remove,
                            TagHandling.Remove,
                            TagHandling.Remove)
                        .ToLowerInvariant();
        text = UrlRegex.Replace(text, string.Empty);
        text = PunctuationRegex.Replace(text, string.Empty);
        var words = text.SplitWhitespace();
        foreach (var word in words)
            if(!word.IsNullOrEmpty())
                Counters.Get("word-" + word).Increment();
        Counters.Get("messages-recieved").Increment();
        var guild = (m.Channel as IGuildChannel)?.Guild;
        if (guild != null) {
          foreach (var shard in client.Shards) {
            if (shard.Guilds.Any(g => g.Id == guild.Id)) {
              Counters.Get($"shard-{shard.ShardId}-messages-recieved").Increment();
              break;
            }
          }
        }
        Counters.Get("messages-attachments").IncrementBy((ulong) um.Attachments.Count);
        Counters.Get("messages-embeds").IncrementBy((ulong) um.Embeds.Count);
        Counters.Get("messages-user-mentions").IncrementBy((ulong) um.MentionedUserIds.Count);
        Counters.Get("messages-channel-mentions").IncrementBy((ulong) um.MentionedChannelIds.Count);
        Counters.Get("messages-role-mentions").IncrementBy((ulong) um.MentionedRoleIds.Count);
        if(m.IsTTS)
            Counters.Get("messages-text-to-speech").Increment();
        return Task.CompletedTask;
    };

    foreach (var shard in client.Shards)
      shard.Connected += () => Increment($"shard-{shard.ShardId}-reconnects");

    client.MessageUpdated += (c, m, ch) => Increment("messages-updated");
    client.MessageDeleted += (i, o) => Increment("messages-deleted");

    client.ChannelCreated += c => Increment("channels-created");
    client.ChannelUpdated += (b, a) => Increment("channels-updated");
    client.ChannelDestroyed += c => Increment("channels-deleted");

    client.RoleCreated += r => Increment("roles-created");
    client.RoleUpdated += (b, a) => Increment("roles-updated");
    client.RoleDeleted += r => Increment("roles-deleted");

    client.UserLeft += u => Increment("user-left");
    client.UserBanned += (u, g) => Increment("user-banned");
    client.UserUnbanned += (u, g) => Increment("user-unbanned");

    client.JoinedGuild += g => Increment("guild-joined");
    client.LeftGuild += g => Increment("guild-left");
  }
}
}
