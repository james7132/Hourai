using Discord;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public static class ServerExtensions {

  public static Task<IGuildUser> GetOwner(this IGuild guild) =>
    guild.GetUserAsync(guild.OwnerId);

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
