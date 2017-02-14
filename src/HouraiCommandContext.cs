using Discord;
using Discord.Commands;
using Hourai.Model;

namespace Hourai {

  public class HouraiCommandContext : CommandContext {

    public User Author { get; }
    public Guild DbGuild { get; }

    public HouraiCommandContext(IDiscordClient client,
                                IUserMessage msg,
                                User author,
                                Guild guild) : base(client, msg) {
      Author = author;
      DbGuild = guild;
    }

  }

}
