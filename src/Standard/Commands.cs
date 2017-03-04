using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Standard {

public partial class Standard {

  [Group("command")]
  [RequireContext(ContextType.Guild)]
  public class Commands : HouraiModule {

    [Log]
    [Command]
    [GuildRateLimit(1, 1)]
    [MinimumRole(MinimumRole.Command)]
    [Remarks("Creates a custom command. Deletes an existing one if response is empty.")]
    public async Task CreateCommand(string name,
                                    [Remainder] string response = "") {
      await Db.Entry(Context.DbGuild).Collection(c => c.Commands).LoadAsync();
      var command = Context.DbGuild.Commands.SingleOrDefault(c => c.Name == name);
      if (string.IsNullOrEmpty(response)) {
        if (command == null) {
          await RespondAsync($"Command {name.Code()} does not exist and thus cannot be deleted.");
          return;
        }
        Context.DbGuild.Commands.Remove(command);
        Db.Commands.Remove(command);
        await Db.Save();
        await RespondAsync($"Custom command {name.Code()} has been deleted.");
        return;
      }
      string action;
      if (command == null) {
        command = new CustomCommand {
          Name = name,
          Response = response,
          Guild = Context.DbGuild
        };
        Context.DbGuild.Commands.Add(command);
        action = "created";
      } else {
        command.Response = response;
        action = "updated";
      }
      await Db.Save();
      await Success($"Command {name.Code()} {action} with response {response}.");
    }

    [Command("dump")]
    [Remarks("Dumps the base source text for a command.")]
    public async Task CommandDump(string command) {
      await Db.Entry(Context.DbGuild).Collection(c => c.Commands).LoadAsync();
      var customCommand = Context.DbGuild.Commands.SingleOrDefault(c => c.Name == command);
      if(customCommand == null)
        await RespondAsync($"No custom command named {command}");
      else
        await RespondAsync($"{customCommand.Name}: {customCommand.Response}");
    }

    [Log]
    [Command("role")]
    [ServerOwner]
    [Remarks("Sets the minimum role for creating custom commands.")]
    public async Task CommandRole(IRole role) {
      const int type = (int)MinimumRole.Command;
      var dbrole = await Db.MinRoles.FindAsync(Context.Guild.Id, type);
      if (dbrole == null) {
        dbrole = new MinRole(MinimumRole.Command, role);
        Db.MinRoles.Add(dbrole);
      }
      dbrole.Role = await Db.Roles.Get(role);
      await Db.Save();
      await Success($"Set {role.Name.Code()} as the minimum role to create custom commnds.");
    }

  }

}

}
