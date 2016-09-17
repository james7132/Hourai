using System.Collections.Generic;
using Discord;
using System.Threading.Tasks;

namespace Hourai {

public class ChannelSet {

  readonly Dictionary<ulong, ChannelLog> _channels;

  public ChannelSet() {
    _channels = new Dictionary<ulong, ChannelLog>();
  }

  public async Task Add(ITextChannel channel) {
    ulong id = channel.Id;
    bool success = !_channels.ContainsKey(id);
    if (success)
      _channels[id] = new ChannelLog(channel);
    await _channels[id].Initialize();
  }

  public ChannelLog Get(ITextChannel channel) {
    ulong id = channel.Id;
    Add(channel);
    return _channels[id];
  }

}

}
