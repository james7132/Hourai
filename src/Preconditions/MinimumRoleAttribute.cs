using Discord;
using Discord.Commands;
using Hourai.Model;
using System;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

public enum MinimumRole : int {
  Command = 0
}

public class MinimumRoleAttribute : PreconditionAttribute {

  readonly MinimumRole _roleType;

  public MinimumRoleAttribute(MinimumRole role) { this._roleType = role; }

  public override async Task<PreconditionResult> CheckPermissions(
      ICommandContext context,
      CommandInfo commandInfo,
      IDependencyMap dependencies) {
    var user = context.User as IGuildUser;
    if(user == null)
      return PreconditionResult.FromError("Must be in server to execute this command");
    if (user.IsBotOwner() || user.IsServerOwner())
      return PreconditionResult.FromSuccess();
    var guild = context.Guild;
    ulong? minRole = dependencies.Get<DatabaseService>().Context.MinRoles.Find(guild.Id, (int)_roleType)?.RoleId;
    if (minRole == null)
      return PreconditionResult.FromError($"{user.Mention} is not the server owner, and no minimum role for {_roleType.ToString().Code()} is set.");
    var role = guild.GetRole(minRole.Value);
    if (role == null) {
      var owner = await guild.GetOwnerAsync();
      return PreconditionResult.FromError($"{owner.Mention} the role for {_roleType.ToString().Code()} no longer exists, and you are the only one who can now run it.");
    }
    if (!Utility.RoleCheck(user, role))
      return PreconditionResult.FromError($"{user.Mention} you do not have the minimum role to run this command. You need at least the {role.Name.Code()} to run it.");
    return PreconditionResult.FromSuccess();
  }

}

}
