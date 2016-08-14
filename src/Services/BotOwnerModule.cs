using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    [Module(AutoLoad = false)]
    public class BotOwnerModule {

        [Command]
        public async Task GetBotLog(IMessage message) {
            //TODO: Reimplement
            await message.Channel.SendMessageAsync("Unimplemented");
            //await e.User.SendFileRetry(Bot.BotLog)
        }

    }
}
