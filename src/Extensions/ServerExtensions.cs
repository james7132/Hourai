using System.Collections.Generic;
using System.Linq;
using Discord;

namespace DrumBot {

public static class ServerExtensions {

  public static IRole GetRole(this IGuild server, string roleName) {
    IRole role = server.Roles.FirstOrDefault(r => r.Name == roleName);
    if (role == null)
      throw new NotFoundException("role", roleName);
    return role;
  }

  public static bool AllowCommands(this IGuild guild)  {
    if(guild == null)
      return false;
#if DEBUG
    return guild.Id == Config.TestServer;
#else
    return guild.Id != Config.TestServer;
#endif
  }

  public static bool AllowCommands(this IChannel channel) {
    if (channel == null)
      return false;
    var gChannel = channel as IGuildChannel;
    return gChannel == null || gChannel.Guild.AllowCommands();
  }

  public static IEnumerable<IRole> Order(this IEnumerable<IRole> roles) => 
    roles.OrderByDescending(r => r.Position);

  public static IEnumerable<IRole> OrderAlpha(this IEnumerable<IRole> roles) => 
    roles.OrderBy(r => r.Name);

  public static IEnumerable<IGuildChannel> Order(this IEnumerable<IGuildChannel> channels) => 
    channels.OrderBy(c => c.Position);

  public static IEnumerable<IGuildChannel> OrderAlpha(this IEnumerable<IGuildChannel> channels) => 
    channels.OrderBy(c => c.Name);

}

}
