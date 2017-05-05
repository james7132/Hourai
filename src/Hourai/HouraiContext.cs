using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Custom;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Hourai {

  internal struct Substring {
    public string String;
    public int Start;
    public int End;
    public IList<Substring> Elements;

    public int Length => End - Start;
    public override string ToString() =>
      String.Substring(Start, Length);
  }

  public class HouraiContext : ICommandContext {

    static readonly KeyValuePair<string, Func<HouraiContext, string>>[] _replacements;

    static HouraiContext() {
      _replacements = new Dictionary<string, Func<HouraiContext, string>> {
        { "$author", c => c?.Message?.Author?.Mention },
        { "$users", c => c?.Users?.Select(u => u.Mention)?.Join(" ") },
        { "$user", c => c?.Users?.FirstOrDefault()?.Mention ??
                        c?.Message?.Author?.Mention
        },
        { "$channels", c => {
          var message = c?.Message as SocketUserMessage;
          var channels = message?.MentionedChannels?.OfType<SocketTextChannel>();
          if (channels != null)
            return channels.Select(ch => ch.Mention).Join(" ");
          return null;
        }},
        { "$channel", c => (c?.Channel as SocketTextChannel)?.Mention ?? c?.Channel?.Name },
        { "$server", c => c?.Guild?.Name }
      }.Select(k => k).ToArray();
    }

    const char StartChar = '(';
    const char EndChar = ')';
    const char ElementChar = '|';
    static readonly char[] searchCharacters = new[] { StartChar, EndChar, ElementChar };

    static readonly Random rand = new Random();

    public string Process(string val) {
      var builder = new StringBuilder(val, val.Length * 2);
      ResolveGroups(builder);
      foreach(var replace in _replacements)
        builder.Replace(replace.Key, replace.Value(this));
      return builder.ToString();
    }

    internal static void ResolveGroups(StringBuilder builder) {
      var changed = false;
      do {
        changed = false;
        foreach(var sub in FindGroups(builder.ToString()).OrderByDescending(s => s.End)) {
          var element = sub.Elements[rand.Next(sub.Elements.Count)];
          builder.Remove(sub.Start - 1, sub.Length + 2);
          builder.Insert(sub.Start - 1, element.ToString());
          changed = true;
        }
      } while(changed);
    }

    internal static IEnumerable<Substring> FindGroups(string text) {
      var stack = 0;
      var parenStart = 0;
      var index = 0;
      var elementStart = -1;
      var elements = new List<Substring>();
      Action addElement = () => elements.Add(new Substring {
                String = text,
                Start = elementStart,
                End = index
              });
      while(index >= 0) {
        index = text.IndexOfAny(searchCharacters, index);
        if(index < 0 || index >= text.Length)
          break;
        switch(text[index]) {
          case StartChar:
            if(stack == 0) {
              parenStart = index + 1;
              elementStart = index + 1;
            }
            stack++;
            break;
          case EndChar:
            if(stack == 1 && elements.Count > 0) {
              addElement();
              yield return new Substring {
                String = text,
                Start = parenStart,
                End = index,
                Elements = elements.ToArray()
              };
              elements.Clear();
            }
            stack = Math.Max(0, stack - 1);
            break;
          case ElementChar:
            if(stack == 1) {
              addElement();
              elementStart = index + 1;
            }
            break;
        }
        index++;
      }
  }

    public User Author { get; }
    public Guild DbGuild { get; }
    public BotDbContext Db { get; }

    public IEnumerable<IUser> Users { get; set;}
    public string Content => Message?.Content;
    public BotCommandService Commands { get; set; }

    public bool IsAutoCommand { get; }

    public string Input { get; set; }
    public SocketGuild Guild { get; set; }
    public SocketUser User { get; set; }
    public ISocketMessageChannel Channel { get; set; }
    public IUserMessage Message { get; set; }
    public DiscordShardedClient Client { get; set; }
    IGuild ICommandContext.Guild => Guild;
    IUser ICommandContext.User => User;
    IMessageChannel ICommandContext.Channel => Channel;
    IDiscordClient ICommandContext.Client => Client;

    public bool IsHelp { get; set; }

    public HouraiContext() {
    }

    public HouraiContext(DiscordShardedClient client,
                         string msg,
                         SocketUser user,
                         ISocketMessageChannel channel,
                         BotDbContext db) {
      Client = client;
      Message = new FakeMessage(msg, user, channel);
      IsAutoCommand = true;
      User = user;
      Channel = channel;
      Guild = (channel as SocketGuildChannel)?.Guild;
      Author = db.Users.Get(user).Result;
      Users = Enumerable.Empty<IUser>();
      if (Guild != null)
        DbGuild = db.Guilds.Get(Guild).Result;
      Db = db;
    }

    public HouraiContext(DiscordShardedClient client,
                                SocketUserMessage msg,
                                BotDbContext db,
                                User author,
                                Guild guild) {
      Client = client;
      Message = msg;
      User = msg.Author;
      Channel = msg.Channel;
      Users = msg.MentionedUsers;
      Guild = (Channel as SocketGuildChannel)?.Guild;
      Author = author;
      DbGuild = guild;
      Db = db;
    }

    public HouraiContext Clone() => (HouraiContext)MemberwiseClone();

  }

}
