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

  //public async Task AddChannel(ITextChannel channel) {
    //ulong id = Check.NotNull(channel).Id;
    //bool success = !_channels.ContainsKey(id);
    //if (success)
      //_channels[id] = new ChannelLog(channel);
    //await _channels[id].Initialize();
  //}

  //public ChannelLog GetChannel(ITextChannel channel) {
    //AddChannel(channel).Wait();
    //return _channels[channel.Id];
  //}

  public GuildLog GetGuild(IGuild guild) {
    ulong id = Check.NotNull(guild).Id;
    bool success = !_guilds.ContainsKey(id);
    if(success)
      _guilds[id] = new GuildLog(guild);
    return _guilds[guild.Id];
  }

}

}
