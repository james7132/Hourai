using System.Collections.Concurrent;
using Discord;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Hourai {

public class LogSet {

  readonly ConcurrentDictionary<ulong, GuildLog> _guilds;
  readonly StorageConfig _config;

  public LogSet(IOptions<StorageConfig> config) {
    _guilds = new ConcurrentDictionary<ulong, GuildLog>();
    _config = config.Value;
  }

  public GuildLog GetGuild(IGuild guild) {
    ulong id = Check.NotNull(guild).Id;
    GuildLog log;
    if(!_guilds.TryGetValue(id, out log)) {
      log = new GuildLog(_config.LogDirectory, guild);
      _guilds[id] = log;
    }
    return log;
  }

}

}
