using System.Collections.Concurrent;
using Discord;
using System.Threading.Tasks;

namespace Hourai {

public class LogSet {

  readonly ConcurrentDictionary<ulong, GuildLog> _guilds;

  public LogSet() {
    _guilds = new ConcurrentDictionary<ulong, GuildLog>();
  }

  public GuildLog GetGuild(IGuild guild) {
    ulong id = Check.NotNull(guild).Id;
    GuildLog log;
    if(!_guilds.TryGetValue(id, out log)) {
      log = new GuildLog(guild);
      _guilds[id] = log;
    }
    return log;
  }

}

}
