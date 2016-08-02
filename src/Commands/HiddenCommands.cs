using Discord.Commands;

namespace DrumBot {

    /// <summary>
    /// Special owner only or debug hidden commands.
    /// </summary>
    class HiddenCommands {

        [Command]
        static async void ServerConfig(CommandEventArgs e) {
            await e.Respond($"{e.User.Mention}\n{e.Server.GetConfig()}");
        }

    }
}
