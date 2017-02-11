using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

public class ServerOwnerAttribute : PreconditionAttribute {

  public override Task<PreconditionResult> CheckPermissions(
      ICommandContext context,
      CommandInfo commandInfo,
      IDependencyMap dependencies) {
    if (QCheck.InGuild(context.Message) == null)
      return Task.FromResult(PreconditionResult.FromError("Not in server."));
    var user = context.Message.Author as IGuildUser;
    if (!user.IsServerOwner() && !user.IsBotOwner())
      return Task.FromResult(PreconditionResult.FromError($"{user.Username} you are not the owner of this server, and thus cannot run {commandInfo.Name.Code()}"));
    return Task.FromResult(PreconditionResult.FromSuccess());
  }

}

}
