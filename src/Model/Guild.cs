using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Model {

[Table("guilds")]
public class Guild {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }

  public bool IsBlacklisted { get; set; }
  [Required]
  public ICollection<Channel> Channels { get; set; }
  [Required]
  public ICollection<GuildUser> Users { get; set; }
  [Required]
  public ICollection<Role> Roles { get; set; }
  [Required]
  public List<CustomCommand> Commands { get; set; }
  //[Required]
  //public List<CounterEvent> Events { get; set; }
  //

  public IList<MinRole> MinRoles { get; set; }

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  [MaxLength(1)]
  public string Prefix { get; set; }

  public Guild() {
    Prefix = Config.CommandPrefix.ToString();
  }

  public Guild(IGuild guild) : this() {
    Id = Check.NotNull(guild).Id;
  }

}

}
