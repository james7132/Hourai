using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

public class LogAttribute : DocumentedPreconditionAttribute {

  public override string GetDocumentation() =>
    "Use of this command will be logged to ``modlog``";

  public override async Task<PreconditionResult> CheckPermissions(
      ICommandContext context,
      CommandInfo commandInfo,
      IDependencyMap dependencies) {
    var hContext = context as HouraiContext;
    if (hContext != null && hContext.IsHelp)
      return PreconditionResult.FromSuccess();
    await dependencies.Get<LogSet>().GetGuild(context.Guild)
      .LogEvent($"{context.User.ToIDString()} used the command {commandInfo.GetFullName().DoubleQuote()} in " +
          $"{context.Channel.ToIDString()}.");
    return PreconditionResult.FromSuccess();
  }

}

}
