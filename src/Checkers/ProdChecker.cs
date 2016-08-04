using Discord;
using Discord.Commands.Permissions;

namespace DrumBot {

    /// <summary>
    /// Abstract class implementation of IPermissionChecker. Also keeps track of the name of the command.
    /// </summary>
    public abstract class Checker : IPermissionChecker {
        /// <summary>
        /// The name of the command.
        /// </summary>
        public string Name { get; set; }

        public abstract bool CanRun(Discord.Commands.Command command, User user, Channel channel, out string error);
    }

    /// <summary>
    /// Checks whether the bot is running a debug or a release version and prevents it from running on the wrong type of server.
    /// </summary>
    public class ProdChecker : Checker {

        public override bool CanRun(Discord.Commands.Command command,
                                       User user,
                                       Channel channel,
                                       out string error) {
            error = string.Empty;
            if (Config.GetServerConfig(channel.Server).AllowCommands) {
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
