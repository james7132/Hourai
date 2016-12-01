using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Hourai.Preconditions;

namespace Hourai.Modules {

public partial class Standard {

  [Group("command")]
  [RequireContext(ContextType.Guild)]
  public class Commands : DatabaseHouraiModule {

    public Commands(BotDbContext db) : base(db) {
    }

    CustomCommand GetCommand(string name) {
      var guild = Database.GetGuild(Check.NotNull(Context.Guild));
      return guild?.GetCustomCommand(name);
    }

    [Command]
    [GuildRateLimit(1, 1)]
    [MinimumRole(MinimumRole.Command)]
    [Remarks("Creates a custom command. Deletes an existing one if response is empty.")]
    public async Task CreateCommand(string name,
                                    [Remainder] string response = "") {
      var guild = Database.GetGuild(Check.NotNull(Context.Guild));
      var command = GetCommand(name);
      if (string.IsNullOrEmpty(response)) {
        if (command == null) {
          await RespondAsync($"Command {name.Code()} does not exist and thus cannot be deleted.");
          return;
        }
        guild.Commands.Remove(command);
        Database.Commands.Remove(command);
        await Database.Save();
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
      await Database.Save();
      await Success($"Command {name.Code()} {action} with response {response}.");
    }

    [Command("dump")]
    [Remarks("Dumps the base source text for a command.")]
    public Task CommandDump(string command) {
      var customCommand = GetCommand(command);
      if(customCommand == null)
        return RespondAsync($"No custom command named {command}");
      else
        return RespondAsync($"{customCommand.Name}: {customCommand.Response}");
    }

    [Command("role")]
    [ServerOwner]
    [Remarks("Sets the minimum role for creating custom commands.")]
    public async Task CommandRole(IRole role) {
      var guild = Database.GetGuild(Check.NotNull(Context.Guild));
      guild.SetMinimumRole(MinimumRole.Command, role);
      await Success($"Set {role.Name.Code()} as the minimum role to create custom commnds");
    }

  }

}

}
