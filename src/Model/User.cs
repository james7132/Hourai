using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hourai {

[Table("users")]
public class User {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  [Required]
  public string Username { get; set; }
  [Required]
  public ICollection<Username> Usernames { get; set; }
  [Required]
  public ICollection<GuildUser> GuildUsers { get; set; }
  //[Required]
  //public ICollection<CounterEvent> Events { get; set; }
  public bool IsBlacklisted { get; set; }
  public ICollection<TempBan> TempBans;

  public User() {
  }

  public User(IUser user) : this() {
    Check.NotNull(user);
    Id = user.Id;
    Usernames = new List<Username>();
    AddName(user.Username);
  }

  public void AddName(string name) {
    Check.NotNull(name);
    if(Username == name)
      return;
    Log.Info($"User updated username: {Username} => {name} ({Id})");
    Username = name;
    var newUsername = new Username {
      User = this,
           Date = DateTimeOffset.Now,
           Name = Username
    };
    Usernames.Add(newUsername);
  }

}

[Table("guild_users")]
public class GuildUser {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong GuildId { get; set; }

  [Required]
  [ForeignKey("Id")]
  public User User;
  [Required]
  [ForeignKey("GuildId")]
  public Guild Guild { get; set; }

  //public ICollection<CounterEvent> Events { get; set; }

  private HashSet<ulong> _bannedRoles;
  public string  BannedRoles {
    get {
      if(_bannedRoles == null || _bannedRoles.Count <= 0)
        return null;
      return JsonConvert.SerializeObject(_bannedRoles);
    }
    set {
      if(string.IsNullOrEmpty(value))
        _bannedRoles = null;
      else
        _bannedRoles = JsonConvert.DeserializeObject<HashSet<ulong>>(value);
    }
  }

  public GuildUser() {
  }

  public GuildUser(IGuildUser user) {
    Id = Check.NotNull(user).Id;
    GuildId = user.Guild.Id;
  }

  public bool IsRoleBanned(IRole role) {
    if(role == null || _bannedRoles == null)
      return false;
    return _bannedRoles.Contains(role.Id);
  }

  public void BanRole(IRole role) {
    if(role == null)
      return;
    if(_bannedRoles == null)
      _bannedRoles = new HashSet<ulong>();
    _bannedRoles.Add(role.Id);
  }

  public bool UnbanRole(IRole role) {
    if(role == null || _bannedRoles == null)
      return false;
    bool success = _bannedRoles.Remove(role.Id);
    if(success && _bannedRoles.Count <= 0)
      _bannedRoles = null;
    return success;
  }

}

[Table("usernames")]
public class Username {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong UserId { get; set; }
  [Required]
  public string Name { get; set; }
  [Required]
  public DateTimeOffset Date { get; set; }

  [ForeignKey("UserId")]
  public User User { get; set; }

}

}
