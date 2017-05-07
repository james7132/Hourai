using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hourai {

[Service]
public class CounterService {

  static readonly Regex UrlRegex = new Regex(@"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*$",
                                          RegexOptions.Compiled |
                                          RegexOptions.IgnoreCase);
  static readonly Regex PunctuationRegex = new Regex(@"[^a-zA-Z\d\s']",
                                              RegexOptions.Compiled);

  readonly CounterSet _counters;

  Task Increment(string key) {
      _counters.Get(key).Increment();
      return Task.CompletedTask;
  }

  public CounterService(DiscordShardedClient client,
                        CounterSet counters,
                        ILoggerFactory loggerFactory) {
    _counters = counters;
    client.MessageReceived += m => {
        var um = m as IUserMessage;
        if (um == null || m.Author?.Id == client?.CurrentUser?.Id)
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
                _counters.Get("word-" + word).Increment();
        _counters.Get("messages-recieved").Increment();
        var guild = (m.Channel as IGuildChannel)?.Guild;
        if (guild != null) {
          _counters.Get($"guild-{guild.Id}_messages-recieved").Increment();
          foreach (var shard in client.Shards) {
            if (shard.Guilds.Any(g => g.Id == guild.Id)) {
              _counters.Get($"shard-{shard.ShardId}-messages-recieved").Increment();
              break;
            }
          }
        }
        _counters.Get("messages-attachments").IncrementBy((ulong) um.Attachments.Count);
        _counters.Get("messages-embeds").IncrementBy((ulong) um.Embeds.Count);
        _counters.Get("messages-user-mentions").IncrementBy((ulong) um.MentionedUserIds.Count);
        _counters.Get("messages-channel-mentions").IncrementBy((ulong) um.MentionedChannelIds.Count);
        _counters.Get("messages-role-mentions").IncrementBy((ulong) um.MentionedRoleIds.Count);
        if(m.IsTTS)
          _counters.Get("messages-text-to-speech").Increment();
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
