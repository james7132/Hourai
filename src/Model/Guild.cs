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

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  [Required]
  [MaxLength(1)]
  public string Prefix { get; set; }

  private Dictionary<MinimumRole, ulong> _minimumRoles;
  public string MinRoles {
    get {
      if(_minimumRoles == null || _minimumRoles.Count <= 0)
        return null;
      return JsonConvert.SerializeObject(_minimumRoles);
    }
    set {
      if(string.IsNullOrEmpty(value))
        _minimumRoles = null;
      else
        _minimumRoles = JsonConvert.DeserializeObject<
          Dictionary<MinimumRole, ulong>>(value);
    }
  }

  public Guild() {
    Prefix = Config.CommandPrefix.ToString();
  }

  public Guild(IGuild guild) : this() {
    Id = Check.NotNull(guild).Id;
  }

  public ulong? GetMinimumRole(MinimumRole roleType) {
    return _minimumRoles?[roleType];
  }

  public void SetMinimumRole(MinimumRole roleType, IRole role) {
    Check.NotNull(role);
    if(_minimumRoles == null)
      _minimumRoles = new Dictionary<MinimumRole, ulong>();
    _minimumRoles[roleType] = role.Id;
  }

}

}
