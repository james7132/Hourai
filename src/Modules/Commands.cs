using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module("command")]
    [PublicOnly]
    [ModuleCheck]
    public class Commands {

        [Command]
        [MinimumRole("command")]
        [Remarks("Creates a custom command. Deletes an existing one if response is empty.")]
        public async Task CreateCommand(IUserMessage message, string name, [Remainder] string response) {
            var serverConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            var command = serverConfig.GetCustomCommand(name);
            if (string.IsNullOrEmpty(response)) {
                if (command == null) {
                    await message.Respond($"CommandUtility {name.Code()} does not exist and thus cannot be deleted.");
                    return;
                }
                serverConfig.RemoveCustomCommand(name);
                await message.Respond($"Custom command {name.Code()} has been deleted.");
                return;
            }
            string action;
            if (command == null) {
                command = serverConfig.AddCustomCommand(name);
                command.Response = response;
                action = "created";
            } else {
                command.Response = response;
                action = "updated";
            }
            serverConfig.Save();
            await message.Success($"CommandUtility {name.Code()} {action} with response {response}.");
        }

        [Command("role")]
        [ServerOwner]
        [Remarks("Sets the minimum role for creating custom commands.")]
        public async Task CommandRole(IUserMessage message, IRole role) {
            var serverConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            serverConfig.SetMinimumRole("command", role);
            await message.Success($"Set {role.Name.Code()} as the minimum role to create custom commnds");
        }

    }
}
