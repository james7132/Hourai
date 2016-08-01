using Discord;
using Discord.Commands;

namespace DrumBot {

    public class LogChecker : Checker {

        public override bool CanRun(Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            Log.Info($"Command { Name } was triggered by { user.Name } on { channel.Server.Name }");
            error = string.Empty;
            return true;
        }
    }
}
