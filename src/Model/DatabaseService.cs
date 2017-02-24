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

  ICollection<ulong> _guilds;

  public BotDbContext CreateContext() {
    return (Context = new BotDbContext());
  }

  public DatabaseService(DiscordShardedClient client) {
    client.ChannelCreated += AddChannel;
    client.ChannelDestroyed += AddChannel;
    client.UserJoined += AddUser;
    client.UserLeft += AddUser;
    //client.JoinedGuild += AddGuild;
    client.MessageReceived += m => AddUser(m.Author);
    _guilds = new HashSet<ulong>();
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
      context.GetGuild(guild_channel.Guild);
      context.GetChannel(guild_channel);
      await context.Save();
    }
  }

  async Task RemoveChannel(SocketChannel channel) {
    var guild_channel = channel as IGuildChannel;
    if (guild_channel == null)
      return;
    using (var context = new BotDbContext()) {
      context.RemoveChannel(guild_channel);
      await context.Save();
    }
  }

  async Task AddGuild(SocketGuild guild) {
    if (_guilds.Contains(guild.Id))
      return;
    using (var context = new BotDbContext()) {
      var dbGuild = context.GetGuild(guild);
      context.AllowSave = false;
      foreach(var channel in guild.Channels) {
        context.GetChannel(channel);
      }
      if (!guild.HasAllMembers)
        await guild.DownloadUsersAsync();
      foreach(var user in guild.Users) {
        if(user.Username == null) {
          Log.Error($"Found user {user.Id} without a username");
          continue;
        }
        context.GetGuildUser(user);
      }
      context.AllowSave = true;
      await context.Save();
    }
    _guilds.Add(guild.Id);
  }

}

}
