using Discord.Commands;

namespace DrumBot {
    class HiddenCommands {

        [Command]
        static async void ServerConfig(CommandEventArgs e) {
            await e.Respond($"{e.User.Mention}\n{e.Server.GetConfig()}");
        }

    }
}
