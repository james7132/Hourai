using Discord;

namespace DrumBot {
    public class BotOwnerChecker : Checker {
        public override bool CanRun(Discord.Commands.Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (!user.IsBotOwner()) {
                error = $"{user.Name} you are not the owner of this bot, and thus cannot run {command.Text}";
                return false;
            }
            return true;
        }
    }
}