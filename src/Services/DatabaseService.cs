using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class DatabaseService {

  public BotDbContext Context { get; private set; }
  DiscordSocketClient Client { get; }

  public BotDbContext CreateContext() {
    return (Context = new BotDbContext());
  }

  public DatabaseService(IDependencyMap map) {
    Client = map.Get<DiscordSocketClient>();
    Client.ChannelCreated += AddChannel;
    Client.UserJoined += AddUser;
    Client.UserLeft += AddUser;
    Client.JoinedGuild += AddGuild;
    Client.MessageReceived += m => AddUser(m.Author);
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
