using Discord;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Hourai.Model {

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

  public User() { }

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
    //Log.Info($"User updated username: {Username} => {name} ({Id})");
    Username = name;
    Usernames.Add(new Username {
      User = this,
      Date = DateTimeOffset.Now,
      Name = Username
    });
  }

}

[Table("guild_users")]
public class GuildUser {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong GuildId { get; set; }

  [Required, ForeignKey("Id")]
  public User User { get; set; }
  [Required, ForeignKey("GuildId")]
  public Guild Guild { get; set; }
  [Required]
  public ICollection<UserRole> Roles { get; set; }

  public ICollection<AbstractTempAction> Actions { get; set; }

  //public ICollection<CounterEvent> Events { get; set; }

  public GuildUser() {
  }

  public GuildUser(IGuildUser user) {
    Id = Check.NotNull(user).Id;
    GuildId = user.Guild.Id;
  }

  public UserRole GetRole(IRole role) {
    var dbRole  = Roles.FirstOrDefault(r => r.UserId == role.Id);
    if (role == null) {
      dbRole = new UserRole {
        UserId = Id,
        GuildId = GuildId,
        RoleId = role.Id
      };
      Roles.Add(dbRole);
    }
    return dbRole;
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
