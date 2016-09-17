using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class BotDbContext : DbContext {

  public DbSet<Guild> Guilds { get; set; }
  public DbSet<Channel> Channels { get; set; }
  public DbSet<User> Users { get; set; }
  public DbSet<GuildUser> GuildUsers { get; set; }
  public DbSet<CustomCommand> Commands { get; set; }

  public bool AllowSave { get; set; } = true;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    Config.Load();
    optionsBuilder.UseSqlite($"Filename={Config.DbFilename}");
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    builder.Entity<Channel>()
      .HasKey(c => new { c.Id, c.GuildId });
    builder.Entity<GuildUser>()
      .HasKey(u => new { u.Id, u.GuildId });
    builder.Entity<CustomCommand>()
      .HasKey(c => new { c.GuildId, c.Name });
    builder.Entity<Username>()
      .HasKey(c => new { c.UserId, c.Date });
  }

  public async Task Save() {
    if(!AllowSave)
      return;
    var changes = await SaveChangesAsync();
    if(changes > 0)
      Log.Info($"Saved {changes} changes to the database.");
  }

  public Guild FindGuild(IGuild iguild) {
    Check.NotNull(iguild);
    return Guilds.Include(g => g.Commands) 
      .Include(g => g.Channels)
      .FirstOrDefault(g => g.Id == iguild.Id); 
  }

  public async Task<Guild> GetGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null) { 
      guild = new Guild(iguild); 
      Guilds.Add(guild);
      await Save();
    }
    return guild;
  }

  public async Task<bool> RemoveGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null)
      return false;
    Guilds.Remove(guild);
    await Save();
    return true;
  }

  public User FindUser(IUser iuser) {
    Check.NotNull(iuser);
    return Users.FirstOrDefault(u => u.Id == iuser.Id);
  }

  public async Task<User> GetUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null) {
      user = new User(iuser);
      Users.Add(user);
      await Save();
    }
    return user;
  }

  public async Task<bool> RemoveUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null)
      return false;
    Users.Remove(user);
    await Save();
    return true;
  }

  public GuildUser FindGuildUser(IGuildUser iuser) {
    Check.NotNull(iuser);
    return GuildUsers.FirstOrDefault(u => 
        (u.Id == iuser.Id) && (u.GuildId == iuser.Guild.Id));
  }

  public async Task<GuildUser> GetGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null) {
      user = new GuildUser(iuser);
      user.User = FindUser(iuser);
      if(user.User == null) {
        user.User = new User(iuser);
        Users.Add(user.User);
      }
      GuildUsers.Add(user);
      await Save();
    }
    return user;
  }

  public async Task<bool> RemoveGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null)
      return false;
    GuildUsers.Remove(user);
    await Save();
    return true;
  }

  public Channel FindChannel(IGuildChannel ichannel) {
    Check.NotNull(ichannel);
    return Channels.FirstOrDefault(c => 
        (c.Id == ichannel.Id) && (c.GuildId == ichannel.Guild.Id));
  }

  public async Task<Channel> GetChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null) {
      channel = new Channel(ichannel);
      Channels.Add(channel);
      await Save();
    }
    return channel;
  }

  public async Task<bool> RemoveChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null)
      return false;
    Channels.Remove(channel);
    await Save();
    return true;
  }

}

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

[Table("channels")]
public class Channel {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong GuildId { get; set; }
  [Required]
  public Guild Guild { get; set; } 
  public bool SearchIgnored { get; set; }
  public bool JoinMessage { get; set; } 
  public bool LeaveMessage { get; set; } 
  public bool BanMessage { get; set; } 

  public Channel() {
  }

  public Channel(IGuildChannel channel) {
    Id = Check.NotNull(channel).Id;
    GuildId = channel.Guild.Id;
  }

}

[Table("users")]
public class User {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  [Required]
  public ICollection<Username> Usernames { get; set; }
  [Required]
  public ICollection<GuildUser> GuildUsers { get; set; }
  public bool IsBlacklisted { get; set; }

  public User()  {
  }

  public User(IUser user) : this() {
    Check.NotNull(user);
    Id = user.Id;
    Usernames = new List<Username> { 
      new Username {
        User = this,
        Date = DateTimeOffset.Now,
        Name = user.Username
      }
    };
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

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong UserId { get; set; }
  public DateTimeOffset Date { get; set; }
  public string Name { get; set; }

  [ForeignKey("UserId")]
  public User User { get; set; }

}

[Table("commands")]
public class CustomCommand {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong GuildId { get; set; }
  public Guild Guild { get; set; }
  public string Name { get; set; }
  public string Response { get; set; }

  public CustomCommand() {
  }

  public Task Execute(IMessage message, string input) {
    var channel = Check.NotNull(message.Channel as ITextChannel);
    return message.Respond(Response.Replace("$input", input)
      .Replace("$user", message.Author.Mention)
      .Replace("$channel", channel.Mention));
  }

}

}
