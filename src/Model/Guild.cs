using Discord;
using Newtonsoft.Json;
using System;
using Hourai.Custom;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Model {

[Table("guilds")]
public class Guild {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
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

[Table("custom_config")]
public class CustomConfig {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong GuildId { get; set; }
  [ForeignKey("GuildId")]
  public Guild Guild { get; set; }

  [Required]
  public string ConfigString { get; set; }

  public CustomConfig() {
  }

  public CustomConfig(IGuild guild) {
    GuildId = guild.Id;
  }

  public void Save(GuildConfig config) {
    ConfigString = config.ToString();
  }

  public static implicit operator GuildConfig(CustomConfig config) {
    return config != null ? GuildConfig.FromString(config.ConfigString) : null;
  }

}

}
