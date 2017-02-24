using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;

namespace Hourai {

  public class HouraiCommandContext : CommandContext {

    public User Author { get; }
    public Guild DbGuild { get; }
    public BotDbContext Db { get; }

    public bool IsHelp { get; set; }

    public new DiscordShardedClient Client => base.Client as DiscordShardedClient;

    public HouraiCommandContext(DiscordShardedClient client,
                                IUserMessage msg,
                                BotDbContext db,
                                User author,
                                Guild guild) : base(client, msg) {
      Author = author;
      DbGuild = guild;
      Db = db;
    }

  }

}
