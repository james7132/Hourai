using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;

namespace Hourai {

  public class HouraiCommandContext : ShardedCommandContext {

    public User Author { get; }
    public Guild DbGuild { get; }
    public BotDbContext Db { get; }

    public bool IsHelp { get; set; }

    public HouraiCommandContext(DiscordShardedClient client,
                                SocketUserMessage msg,
                                BotDbContext db,
                                User author,
                                Guild guild) : base(client, msg) {
      Author = author;
      DbGuild = guild;
      Db = db;
    }

  }

}
