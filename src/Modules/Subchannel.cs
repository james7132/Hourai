using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    [ModuleCheck]
    public class Subchannel {

        [Command("soon")]
        public async Task Soon(IUserMessage message) {
            await message.Channel.SendMessageAsync("Subchannel emulation: Coming soon™");
        }

    }
}
