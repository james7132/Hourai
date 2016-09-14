using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrumBot {

public class BotDbContext : DbContext {

  public DbSet<Guild> Guilds { get; set; }
  public DbSet<Channel> Channels { get; set; }
  public DbSet<GuildUser> Users { get; set; }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    optionsBuilder.UseSqlite("Filename=./bot.db");
  }

}

public interface IEditable {
  event Action OnEdit;
}

public abstract class SnowflakeEntity {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }

}

public abstract class GuildEntity : SnowflakeEntity {

  public ulong GuildId { get; set; }

}

public class Guild : SnowflakeEntity {

  public bool IsBlacklisted { get; set; }

}

public class Channel : GuildEntity {

  public bool SearchIgnored { get; set; }

}

public class GuildUser : GuildEntity {

  public bool IsBlacklisted { get; set; }

}

public class ChannelConfig : IEditable {

  [JsonIgnore]
  public ulong Id { get; set; }

  [JsonProperty]
  bool isIgnored = false;

  public event Action OnEdit;

  public ChannelConfig(ulong id) { this.Id = id; }

  [JsonIgnore]
  public bool IsIgnored {
    get { return isIgnored; }
    set {
      bool changed = isIgnored == value;
      isIgnored = value; 
      if(changed)
        OnEdit?.Invoke();
    }
  }

  public void Ignore() {
    bool changed = !isIgnored;
    isIgnored = true;
    if(changed)
      OnEdit?.Invoke();
  }

  public void Unignore() {
    bool changed = isIgnored;
    isIgnored = false;
    if(changed)
      OnEdit?.Invoke();
  }

}

public class UserConfig : IEditable {

  public event Action OnEdit;

  [JsonIgnore]
  public ulong Id { get; set; }

  [JsonProperty]
  HashSet<ulong> bannedRoles;

  public UserConfig(ulong id) {
    Id = id;
    bannedRoles = new HashSet<ulong>();
  }

  public async Task RemoveBannedRoles(IGuildUser user) {
    if (bannedRoles.Count <= 0)
      return;
    await user.RemoveRolesAsync(user.Roles.Where(r => bannedRoles.Contains(r.Id)));
  }

  public bool IsRoleBanned(IRole role) {
    return bannedRoles.Contains(role.Id);
  }

  public bool BanRole(IRole role) {
    bool success = bannedRoles.Add(Check.NotNull(role).Id);
    if(success)
      OnEdit?.Invoke();
    return success;
  }

  public bool UnbanRole(IRole role) {
    bool success = bannedRoles.Remove(Check.NotNull(role).Id);
    if(success)
      OnEdit?.Invoke();
    return success;
  }

}

public class CustomCommand : IEditable {

    public event Action OnEdit;

    [JsonProperty]
    string response;

    [JsonIgnore]
    public string Response {
      get { return response; }
      set {
          bool changed = response != value;
          response = value;
          if(changed)
              OnEdit?.Invoke();
      }
    }

    public async Task Execute(IMessage message, string input) {
      var channel = Check.NotNull(message.Channel as ITextChannel);
      await message.Respond(response.Replace("$input", input)
                              .Replace("$user", message.Author.Mention)
                              .Replace("$channel", channel.Mention));
    }

}

public class GuildConfig : JsonSaveable {

  [JsonIgnore]
  public ulong ID { get; set; }

  [JsonProperty]
  Dictionary<ulong, ChannelConfig> ChannelConfigs { get; set; }

  [JsonProperty]
  Dictionary<ulong, UserConfig> UserConfigs { get; set; }

  [JsonProperty]
  public Dictionary<string, CustomCommand> CustomCommands { get; set; }

  [JsonIgnore]
  public IEnumerable<CustomCommand> Commands => CustomCommands.Values;

  [JsonProperty]
  Dictionary<string, ulong> MinimumRoles { get; set; }

  [JsonIgnore]
  public IGuild Server { get; }
  public HashSet<string> Modules { get; set; }

  [JsonProperty]
  public bool IsBlacklisted { get; set; }

  public GuildConfig(IGuild server) {
    Server = server;
    ID = server.Id;
    Modules = new HashSet<string>();
    ChannelConfigs = new Dictionary<ulong, ChannelConfig>();
    UserConfigs = new Dictionary<ulong, UserConfig>();
    CustomCommands = new Dictionary<string, CustomCommand>();
    MinimumRoles = new Dictionary<string, ulong>();
    Log.Info($"Loading server configuration for { server.ToIDString() } from { SaveLocation }");
    LoadIfFileExists();
  }

  public ChannelConfig GetChannelConfig(IChannel channel) {
    var id = Check.NotNull(channel).Id;
    if (!ChannelConfigs.ContainsKey(id)) {
      var config = new ChannelConfig(channel.Id);
      config.OnEdit += Save;
      ChannelConfigs[id] = config;
    }
    return ChannelConfigs[id];
  }

  public UserConfig GetUserConfig(IGuildUser user) {
    var id = Check.NotNull(user).Id;
    if (!UserConfigs.ContainsKey(id)) {
      var config = new UserConfig(id);
      config.OnEdit += Save;
      UserConfigs[id] = config;
    }
    return UserConfigs[id];
  }

  public void SetMinimumRole(string name, IRole minimumRole) {
    MinimumRoles[name] = minimumRole.Id;
    Save();
  }

  public ulong? GetMinimumRole(string name) {
    if (!MinimumRoles.ContainsKey(name))
      return null;
    return MinimumRoles[name];
  }

  public CustomCommand GetCustomCommand(string name) {
    name = name.ToLowerInvariant();
    if (CustomCommands.ContainsKey(name))
      return CustomCommands[name];
    return null;
  }

  public CustomCommand AddCustomCommand(string name) {
    name = name.ToLowerInvariant();
    var command = new CustomCommand();
    CustomCommands.Add(name, command);
    Save();
    return command;
  }

  public void RemoveCustomCommand(string name) {
    if(CustomCommands.Remove(name))
      Save();
  }

  protected override IEnumerable<IEditable> Editables {
    get {
      foreach (var config in ChannelConfigs) {
        config.Value.Id = config.Key;
        yield return config.Value;
      }
      foreach (var config in UserConfigs) {
        config.Value.Id = config.Key;
        yield return config.Value;
      }
    }
  }

  public void AddModule(string name) {
    if (Modules.Add(name.ToLowerInvariant()))
      Save();
  }

  public bool IsModuleEnabled(string name) {
    return Modules.Contains(name.ToLowerInvariant());
  }

  public void RemoveModule(string name) {
    if (Modules.Remove(name.ToLowerInvariant()))
      Save();
  }
  
  [JsonIgnore]
  public bool AllowCommands {
    get {
#if DEBUG
      return ID == Config.TestServer;
#else
      return ID != Config.TestServer;
#endif
    }
  }

  protected override string DirectoryName => Config.ConfigDirectory;
  protected override string FileName => ID + ".config";

}

}
