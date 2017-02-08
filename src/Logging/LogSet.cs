using System.Collections.Generic;
using Discord;
using System.Threading.Tasks;

namespace Hourai {

public class LogSet {

  //readonly Dictionary<ulong, ChannelLog> _channels;
  readonly Dictionary<ulong, GuildLog> _guilds;

  public LogSet() {
    //_channels = new Dictionary<ulong, ChannelLog>();
    _guilds = new Dictionary<ulong, GuildLog>();
  }

  public GuildLog GetGuild(IGuild guild) {
    ulong id = Check.NotNull(guild).Id;
    bool success = !_guilds.ContainsKey(id);
    if(success)
      _guilds[id] = new GuildLog(guild);
    return _guilds[guild.Id];
  }

}

}
