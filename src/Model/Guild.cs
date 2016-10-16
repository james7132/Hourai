using Discord;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Hourai {

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
  public List<CustomCommand> Commands { get; set; }

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

  public ModuleType Modules { get; set; }

  public Guild() {
    Modules = (ModuleType) ~0L;
  }

  public Guild(IGuild guild) : this() {
    Id = Check.NotNull(guild).Id;
  }

  public IGuild GetDiscordGuild() {
    return Bot.Client.GetGuild(Id);
  }

  public bool AddModule(ModuleType module) {
    var before = Modules;
    Modules |= module;
    return Modules != before;
  }

  public bool IsModuleEnabled(ModuleType module) {
    return (Modules & module) != 0;
  }

  public bool RemoveModule(ModuleType module) {
    var before = Modules;
    Modules &= ~module;
    return Modules != before;
  }

  public CustomCommand GetCustomCommand(string name) {
    return Commands?.Find(c => c.Name == name);
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
