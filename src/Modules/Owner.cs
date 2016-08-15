using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    [BotOwner]
    public class Owner {

        [Command("getbotlog")]
        [PrivateOnly]
        [Description("Gets the log for the bot.")]
        public async Task GetBotLog(IMessage message) {
            await message.Channel.SendFileRetry(Bot.BotLog);
        }

        [Command("kill")]
        [Description("Turns off the bot.")]
        public async Task Kill(IMessage message) {
            await message.Success();
            Environment.Exit(-1);
        }

    }
}
