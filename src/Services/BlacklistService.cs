using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace Hourai {

public class BlacklistService {

  DiscordSocketClient Client { get; }
  BotDbContext Database { get; }

  public BlacklistService(IDependencyMap dependencies) {
    Client = dependencies.Get<DiscordSocketClient>();
    Database = dependencies.Get<BotDbContext>();
    Client.GuildAvailable += CheckBlacklist(false);
    Client.JoinedGuild += CheckBlacklist(true);
  }

  Func<IGuild, Task> CheckBlacklist(bool normalJoin) {
    return async guild => {
      var config = await Database.GetGuild(guild);
      var defaultChannel = (await guild.GetChannelAsync(guild.DefaultChannelId)) as ITextChannel;
      if(config.IsBlacklisted) {
        Log.Info($"Added to blacklisted guild {guild.Name} ({guild.Id})");
        await defaultChannel.Respond("This server has been blacklisted by this bot. " +
            "Please do not add it again. Leaving...");
        await guild.LeaveAsync();
        return;
      }
      if(normalJoin) {
        var help = $"{config.Prefix}help".Code();
        var module = $"{config.Prefix}module".Code();
        await defaultChannel.Respond(
            $"Hello {guild.Name}! {Bot.User.Username} has been added to your server!\n" +
            $"To see available commands, run the command {help}\n" +
            $"For more information, see https://github.com/james7132/Hourai");
      }
    };
  }

}

}
