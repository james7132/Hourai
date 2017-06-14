using Discord;
using Discord.WebSocket;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Hourai.Model {

[Table("temp_actions")]
public class AbstractTempAction {
[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Id { get; set; }
  [Required]
  public ulong UserId { get; set; }
  [Required]
  public ulong GuildId { get; set; }
  [Required]
  public DateTimeOffset Expiration { get; set; }
  [Required]
  public bool Reverse { get; set; } = false;

  public GuildUser User { get; set; }

  public virtual Task Apply(DiscordShardedClient client) {
    throw new NotImplementedException();
  }

  public virtual Task Unapply(DiscordShardedClient client) {
    throw new NotImplementedException();
  }

  public SocketGuildUser GetUser(DiscordShardedClient client) =>
    client.GetGuild(GuildId)?.GetUser(UserId);

}

public class TempBan : AbstractTempAction {

  public override async Task Apply(DiscordShardedClient client) {
    var guild = client.GetGuild(GuildId);
    await guild.AddBanAsync(UserId);
  }

  public override async Task Unapply(DiscordShardedClient client) {
    var guild = client.GetGuild(GuildId);
    await guild.RemoveBanAsync(UserId);
    //Log.Info($"{UserId}'s temp ban from {GuildId} has been lifted.");
  }

}

public class TempMute : AbstractTempAction {

  public override async Task Apply(DiscordShardedClient client) {
    var user = GetUser(client);
    if (user == null)
      throw new InvalidOperationException("User not found");
    await user.MuteAsync();
  }

  public override async Task Unapply(DiscordShardedClient client) {
    var user = GetUser(client);
    if (user == null)
      throw new InvalidOperationException("User not found");
    await user.UnmuteAsync();
  }

}

public class TempDeafen : AbstractTempAction {

  public override async Task Apply(DiscordShardedClient client) {
    var user = GetUser(client);
    if (user == null)
      throw new InvalidOperationException("User not found");
    await user.DeafenAsync();
  }

  public override async Task Unapply(DiscordShardedClient client) {
    var user = GetUser(client);
    if (user == null)
      throw new InvalidOperationException("User not found");
    await user.UndeafenAsync();
  }
}

public class TempRole : AbstractTempAction {

  [Required]
  public ulong RoleId { get; set; }
  public Role Role { get; set; }

  public override async Task Apply(DiscordShardedClient client) {
    var guild = client.GetGuild(GuildId);
    var user = guild.GetUser(UserId);
    var role = guild.GetRole(RoleId);
    if(user == null || role == null)
      return;
    await user.AddRoleAsync(role);
  }

  public override async Task Unapply(DiscordShardedClient client) {
    var guild = client.GetGuild(GuildId);
    var user = guild?.GetUser(UserId);
    var role = guild?.GetRole(RoleId);
    if(user == null || role == null)
      return;
    await user.RemoveRoleAsync(role);
  }

}

}
