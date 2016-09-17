using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

[Module("command")]
[PublicOnly]
[ModuleCheck(ModuleType.Command)]
public class Commands {

  [Command]
  [MinimumRole(MinimumRole.Command)]
  [Remarks("Creates a custom command. Deletes an existing one if response is empty.")]
  public async Task CreateCommand(IUserMessage message, 
                                  string name, 
                                  [Remainder] string response = "") {
    var guild = await Bot.Database.GetGuild(Check.InGuild(message).Guild);
    var command = guild.GetCustomCommand(name);
    if (string.IsNullOrEmpty(response)) {
      if (command == null) {
        await message.Respond($"CommandUtility {name.Code()} does not exist and thus cannot be deleted.");
        return;
      }
      guild.Commands.Remove(command);
      await message.Respond($"Custom command {name.Code()} has been deleted.");
      return;
    }
    string action;
    if (command == null) {
      command = new CustomCommand {
        Name = name,
        Response = response,
        Guild = guild
      };
      guild.Commands.Add(command);
      action = "created";
    } else {
      command.Response = response;
      action = "updated";
    }
    await Bot.Database.Save();
    await message.Success($"CommandUtility {name.Code()} {action} with response {response}.");
  }

  [Command("role")]
  [ServerOwner]
  [Remarks("Sets the minimum role for creating custom commands.")]
  public async Task CommandRole(IUserMessage message, IRole role) {
    var guild = await Bot.Database.GetGuild(Check.InGuild(message).Guild);
    guild.SetMinimumRole(MinimumRole.Command, role);
    await message.Success($"Set {role.Name.Code()} as the minimum role to create custom commnds");
  }

}

}
