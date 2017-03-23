using Discord.Commands;
using System.Linq;

namespace Hourai  {

  public static class CommandInfos {

    public static string GetFullName(this ModuleInfo module) =>
      module?.Aliases?.First();

    public static string GetFullName(this CommandInfo command) =>
      command?.Aliases?.First();

  }

}
