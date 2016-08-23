using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    public class Subchannel {

        [Command("soon")]
        public async Task Soon(IMessage message) {
            await message.Channel.SendMessageAsync("Subchannel emulation: Coming soon™");
        }

    }
}
