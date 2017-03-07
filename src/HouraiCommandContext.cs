using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Custom;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Hourai {

  public class HouraiContext : ICommandContext {

    static readonly Dictionary<string, Func<HouraiContext, string>> _replacements;

    static HouraiContext() {
      _replacements = new Dictionary<string, Func<HouraiContext, string>> {
        { "$author", c => c?.Message?.Author?.Mention },
        { "$users", c => c?.Users?.Select(u => u.Mention)?.Join(" ") },
        { "$channel", c => (c?.Channel as SocketTextChannel)?.Mention ?? c?.Channel?.Name },
        { "$server", c => c?.Guild?.Name }
      };
    }

    public string Process(string val) {
      foreach(var replace in _replacements)
        val = val.Replace(replace.Key, replace.Value(this));
      return val;
    }

    public User Author { get; }
    public Guild DbGuild { get; }
    public BotDbContext Db { get; }

    public IEnumerable<IUser> Users => (Message as SocketUserMessage)?.MentionedUsers ?? Enumerable.Empty<IUser>();
    public string Content => Message?.Content;
    public CustomConfigService ConfigService { get; set; }

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
      User = user;
      Channel = channel;
      Guild = (channel as SocketGuildChannel)?.Guild;
      Author = db.Users.Get(user).Result;
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
      Guild = (Channel as SocketGuildChannel)?.Guild;
      Author = author;
      DbGuild = guild;
      Db = db;
    }

  }

}
