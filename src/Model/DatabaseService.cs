using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Model {

public class DatabaseService {

  [NotService]
  public BotDbContext Context { get; private set; }

  public BotDbContext CreateContext() {
    return (Context = new BotDbContext());
  }

  public DatabaseService(DiscordShardedClient client) {
    client.ChannelCreated += AddChannel;
    client.UserJoined += AddUser;
    client.UserLeft += AddUser;
    client.JoinedGuild += AddGuild;
    client.MessageReceived += m => AddUser(m.Author);
  }

  async Task AddUser(IUser iuser) {
    if(iuser.Username == null)
      return;
    using (var context = new BotDbContext()) {
      var user = context.GetUser(iuser);
      user.AddName(iuser.Username);
      await context.Save();
    }
  }

  async Task AddChannel(SocketChannel channel) {
    var guild_channel = channel as IGuildChannel;
    if (guild_channel == null)
      return;
    using (var context = new BotDbContext()) {
      context.GetChannel(guild_channel);
      await context.Save();
    }
  }

  async Task AddGuild(SocketGuild guild) {
    using (var context = new BotDbContext()) {
      var dbGuild = context.GetGuild(guild);
      foreach(var channel in guild.Channels) {
        context.GetChannel(channel);
      }
      await guild.DownloadUsersAsync();
      foreach(var user in guild.Users) {
        if(user.Username == null) {
          Log.Error($"Found user {user.Id} without a username");
          continue;
        }
        context.GetGuildUser(user);
      }
      await context.Save();
    }
  }

}

}
