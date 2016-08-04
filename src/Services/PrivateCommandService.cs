using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;

namespace DrumBot {
    public class PrivateCommandService : IService {
        public void Install(DiscordClient client) {
            client.GetService<CommandService>()
                .CreateCommand("getbotlog")
                .PrivateOnly()
                .AddCheck(new BotOwnerChecker())
                .Do(async e => await e.User.SendFileRetry(Bot.BotLog));
        }
    }
}
