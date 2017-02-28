using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Model {

[Table("roles")]
public class Role {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  [Required]
  public ulong GuildId { get; set; }

  [Required, ForeignKey("GuildId")]
  public Guild Guild { get; set; }
  [Required]
  public ICollection<UserRole> Users { get; set; }

  public Role() {
  }

  public Role(IRole role) {
    Id = Check.NotNull(role).Id;
    GuildId = Check.NotNull(role.Guild).Id;
  }

}

[Table("min_roles")]
public class MinRole {

  public ulong GuildId { get; set; }
  public int Type { get; set; }
  public ulong RoleId { get; set; }

  [Required, ForeignKey("RoleId")]
  public Role Role { get; set; }
  [Required, ForeignKey("GuildId")]
  public Guild Guild { get; set;}

  public MinRole() {
  }

  public MinRole (MinimumRole type, IRole role) {
    RoleId = role.Id;
    GuildId = role.Guild.Id;
    Type = (int)type;
  }

}

[Table("user_rolesj")]
public class UserRole {

  [Required]
  public ulong UserId { get; set; }
  [Required]
  public ulong GuildId { get; set; }
  [Required]
  public ulong RoleId { get; set; }

  [Required]
  public GuildUser User { get; set; }
  [Required, ForeignKey("RoleId")]
  public Role Role { get; set; }

  public UserRole() {
  }

  public UserRole(IGuildUser user, IRole role) {
    UserId = user.Id;
    GuildId = user.Guild.Id;
    RoleId = role.Id;
  }

  public bool HasRole { get; set; }
  public bool IsBanned { get; set; }

}

}
