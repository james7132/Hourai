using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Model {

[Service]
public class DatabaseService {

  IServiceProvider _services;
  ICollection<ulong> _guilds;

  public DatabaseService(DiscordShardedClient client,
                         IServiceProvider services) {
    _services = services;
    client.ChannelCreated += AddChannel;
    client.ChannelDestroyed += AddChannel;
    client.UserJoined += AddUser;
    client.UserLeft += AddUser;
    client.UserUpdated += (b, a) => AddUser(a);
    //client.GuildMemberUpdated += (b, a) => RefreshUser(a);
    client.RoleCreated += r => AddRole(r);
    client.RoleDeleted += r => Remove(c => c.Roles, r);
    //client.JoinedGuild += AddGuild;
    client.MessageReceived += m => AddUser(m.Author);
    _guilds = new HashSet<ulong>();
  }

  async Task Add<T>(Func<BotDbContext, DbSet<T>> setFunc, IEntity<ulong> entity) where T :class {
    using (var context = _services.GetService<BotDbContext>()) {
      await setFunc(context).Get(entity);
      await context.Save();
    }
  }

  async Task Remove<T>(Func<BotDbContext, DbSet<T>> setFunc, IEntity<ulong> entity) where T :class {
    using (var context = _services.GetService<BotDbContext>()) {
      await setFunc(context).Remove(entity);
      await context.Save();
    }
  }

  async Task AddRole(IRole role) {
    using (var context = _services.GetService<BotDbContext>()) {
      await context.Guilds.Get(role.Guild);
      await context.Roles.Get(role);
      await context.Save();
    }
  }

  async Task AddUser(IUser iuser) {
    if(iuser.Username == null)
      return;
    using (var context = _services.GetService<BotDbContext>()) {
      var user = await context.Users.Get(iuser);
      await context.Entry(user).Collection(u => u.Usernames).LoadAsync();
      user.AddName(iuser.Username);
      await context.Save();
    }
  }

  async Task AddChannel(SocketChannel channel) {
    var guild_channel = channel as IGuildChannel;
    if (guild_channel == null)
      return;
    using (var context = _services.GetService<BotDbContext>()) {
      await context.Guilds.Get(guild_channel.Guild);
      await context.Channels.Get(guild_channel);
      await context.Save();
    }
  }

  //async Task RefreshUser(SocketGuildUser user) {
    //using (var context = new BotDbContext()) {
      //await context.RefreshUser(user);
      //await context.Save();
    //}
  //}

  async Task RemoveChannel(SocketChannel channel) {
    var guild_channel = channel as IGuildChannel;
    if (guild_channel == null)
      return;
    using (var context = _services.GetService<BotDbContext>()) {
      await context.Channels.Remove(guild_channel);
      await context.Save();
    }
  }

  async Task AddGuild(SocketGuild guild) {
    if (_guilds.Contains(guild.Id))
      return;
    using (var context = _services.GetService<BotDbContext>()) {
      await context.RefreshGuild(guild);
      await context.Save();
    }
    _guilds.Add(guild.Id);
  }

}

}
