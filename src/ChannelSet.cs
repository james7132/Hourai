using System.Collections.Generic;
using Discord;

namespace DrumBot {
    public class ChannelSet {

        readonly Dictionary<ulong, ChannelLog> _channels;

        public ChannelSet() {
            _channels = new Dictionary<ulong, ChannelLog>();
        }

        public ChannelLog Get(Channel channel) {
            ulong id = channel.Id;
            if (!_channels.ContainsKey(id))
                _channels[id] = new ChannelLog(channel);
            return _channels[id];
        }

    }
}
