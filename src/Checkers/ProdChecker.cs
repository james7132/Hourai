using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;

namespace DrumBot {

    public abstract class Checker : IPermissionChecker {
        public string Name { get; set; }

        public abstract bool CanRun(Discord.Commands.Command command, User user, Channel channel, out string error);
    }

    public class ProdChecker : Checker {

        public override bool CanRun(Discord.Commands.Command command,
                                       User user,
                                       Channel channel,
                                       out string error) {
            error = string.Empty;
            if (Bot.Config.GetServerConfig(channel.Server).AllowCommands) {
#if DEBUG
                Log.Info($"Command { Name } is running in DEBUG mode on a TEST server and can execute");
#endif
                return true;
            }
            Log.Info($"Command \"{ Name }\" cannot be used in { channel.Server.Name } as it is not a PROD server.");
            return false;
        }
    }
}
