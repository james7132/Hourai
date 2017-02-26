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

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
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
  }

}

[Table("user_role")]
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

  [Required]
  public bool HasRole { get; set; }

  [Required]
  public bool IsBanned { get; set; }

}

}
