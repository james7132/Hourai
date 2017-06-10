using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

public class LogAttribute : DocumentedPreconditionAttribute {

  public override string GetDocumentation() =>
    "*Use of this command will be logged to ``modlog``.*";

  public override async Task<PreconditionResult> CheckPermissions(
      ICommandContext context,
      CommandInfo commandInfo,
      IServiceProvider services) {
    var hContext = context as HouraiContext;
    if (hContext != null && hContext.IsHelp)
      return PreconditionResult.FromSuccess();
    await services.GetService<LogSet>().GetGuild(context.Guild)
      .LogEvent($"{context.User.ToIDString()} used the command {context.Message.Content} in " +
          $"{context.Channel.ToIDString()}.");
    return PreconditionResult.FromSuccess();
  }

}

}
