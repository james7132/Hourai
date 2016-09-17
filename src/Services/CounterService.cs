using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Hourai {

public class CounterService {

  CounterSet Counters { get; }

  static readonly Regex UrlRegex = new Regex(@"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*$",
                                          RegexOptions.Compiled |
                                          RegexOptions.IgnoreCase);
  static readonly Regex PunctuationRegex = new Regex(@"[^a-zA-Z\d\s']",
                                              RegexOptions.Compiled);

  Task Increment(string key) {
     Counters.Get(key).Increment();
     return Task.CompletedTask;
  }

  public CounterService(DiscordSocketClient client, CounterSet counters) {
    Counters = counters;

    client.MessageReceived += m => {
        var um = m as IUserMessage;
        if (um == null || m.Author.IsMe())
            return Task.CompletedTask;
        var text =  um.Resolve(UserMentionHandling.Remove, 
                            ChannelMentionHandling.Remove, 
                            RoleMentionHandling.Remove, 
                            EveryoneMentionHandling.Remove)
                        .ToLowerInvariant();
        text = UrlRegex.Replace(text, string.Empty);
        text = PunctuationRegex.Replace(text, string.Empty);
        var words = text.SplitWhitespace();
        foreach (var word in words)
            if(!word.IsNullOrEmpty())
                Counters.Get("word-" + word).Increment();
        Counters.Get("messages-recieved").Increment();
        Counters.Get("messages-attachments").IncrementBy((ulong) um.Attachments.Count);
        Counters.Get("messages-embeds").IncrementBy((ulong) um.Embeds.Count);
        Counters.Get("messages-user-mentions").IncrementBy((ulong) um.MentionedUsers.Count);
        Counters.Get("messages-channel-mentions").IncrementBy((ulong) um.MentionedChannelIds.Count);
        Counters.Get("messages-role-mentions").IncrementBy((ulong) um.MentionedRoles.Count);
        if(m.IsTTS)
            Counters.Get("messages-text-to-speech").Increment();
        return Task.CompletedTask;
    };

    client.MessageUpdated +=
        delegate { return Increment("messages-updated"); };
    client.MessageDeleted +=
        delegate { return Increment("messages-deleted"); };

    client.ChannelCreated +=
        delegate { return Increment("channels-created"); };
    client.ChannelUpdated +=
        delegate { return Increment("channels-updated"); };
    client.ChannelDestroyed +=
        delegate { return Increment("channels-deleted"); };

    client.RoleCreated +=
        delegate { return Increment("roles-created"); };
    client.RoleUpdated +=
        delegate { return Increment("roles-updated"); };
    client.RoleDeleted +=
        delegate { return Increment("roles-deleted"); };

    client.UserLeft+= delegate { return Increment("user-left"); };
    client.UserBanned += delegate { return Increment("user-banned"); };
    client.UserUnbanned +=
        delegate { return Increment("user-unbanned"); };

    client.JoinedGuild += delegate { return Increment("guild-joined"); };
    client.LeftGuild += delegate { return Increment("guild-left"); };
  }
}
}
