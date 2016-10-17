using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Hourai {

[Group("command")]
[Hide]
[PublicOnly]
[ModuleCheck(ModuleType.Command)]
public class Commands : HouraiModule {

  [Command]
  [MinimumRole(MinimumRole.Command)]
  [Remarks("Creates a custom command. Deletes an existing one if response is empty.")]
  public async Task CreateCommand(string name, 
                                  [Remainder] string response = "") {
    var guild = await Bot.Database.GetGuild(Check.NotNull(Context?.Guild));
    var command = guild.GetCustomCommand(name);
    if (string.IsNullOrEmpty(response)) {
      if (command == null) {
        await RespondAsync($"CommandUtility {name.Code()} does not exist and thus cannot be deleted.");
        return;
      }
      guild.Commands.Remove(command);
      Bot.Database.Commands.Remove(command);
      await RespondAsync($"Custom command {name.Code()} has been deleted.");
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
    await Success($"CommandUtility {name.Code()} {action} with response {response}.");
  }

  [Command("role")]
  [ServerOwner]
  [Remarks("Sets the minimum role for creating custom commands.")]
  public async Task CommandRole(IRole role) {
    var guild = await Bot.Database.GetGuild(Check.NotNull(Context?.Guild));
    guild.SetMinimumRole(MinimumRole.Command, role);
    await Success($"Set {role.Name.Code()} as the minimum role to create custom commnds");
  }

}

}
