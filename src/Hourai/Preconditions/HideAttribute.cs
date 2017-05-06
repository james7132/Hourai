using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

  [AttributeUsage(AttributeTargets.Method)]
  public class HideAttribute : PreconditionAttribute {

    public override Task<PreconditionResult> CheckPermissions(
        ICommandContext context,
        CommandInfo commandInfo,
        IServiceProvider service) {
      var hContext = context as HouraiContext;
      if (hContext == null || !hContext.IsHelp)
        return Task.FromResult(PreconditionResult.FromSuccess());
      return Task.FromResult(PreconditionResult.FromError("Help command, cannot run"));
    }

  }

}
