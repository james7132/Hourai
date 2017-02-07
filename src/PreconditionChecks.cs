using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Hourai {

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

  [Flags]
  public enum MinimumRole : long {
    Command = 1 << 0
  }

  public class MinimumRoleAttribute : PreconditionAttribute {
    readonly MinimumRole _roleType;

    public MinimumRoleAttribute(MinimumRole role) { this._roleType = role; }

    public override async Task<PreconditionResult> CheckPermissions(
        ICommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      var user = context.Message.Author as IGuildUser;
      if(user == null)
        return PreconditionResult.FromError("Must be in server to execute this command");
      if (user.IsBotOwner() || user.IsServerOwner())
        return PreconditionResult.FromSuccess();
      var server = user.Guild;
      var guild = dependencies.Get<DatabaseService>().Context.GetGuild(server);
      ulong? minRole = guild.GetMinimumRole(_roleType);
      if (minRole == null)
        return PreconditionResult.FromError($"{user.Mention} is not the server owner, and no minimum role for {_roleType.ToString().Code()} is set.");
      var role = server.GetRole(minRole.Value);
      if (role == null) {
        var owner = await server.GetOwnerAsync();
        return PreconditionResult.FromError($"{owner.Mention} the role for {_roleType.ToString().Code()} no longer exists, and you are the only one who can now run it.");
      }
      if (!Utility.RoleCheck(user, role))
        return PreconditionResult.FromError($"{user.Mention} you do not have the minimum role to run this command. You need at least the {role.Name.Code()} to run it.");
      return PreconditionResult.FromSuccess();
    }
  }

  public static class Check {
    public static T NotNull<T>(T obj) {
      if (obj == null)
        throw new ArgumentNullException();
      return obj;
    }

    public static ITextChannel InGuild(IMessage message) {
      if(!(message.Channel is ITextChannel))
        throw new Exception("CommandUtility must be executed in a public channel");
      return (ITextChannel) message.Channel;
    }

    public static IDMChannel InPrivate(IMessage message) {
      if(!(message.Channel is IDMChannel))
        throw new Exception("CommandUtility must be executed in a private channel");
      return (IDMChannel) message.Channel;
    }
  }

  public static class QCheck {
    public static ITextChannel InGuild(IMessage message) {
      return message.Channel as ITextChannel;
    }

    public static IDMChannel InPrivate(IMessage message) {
      return message.Channel as IDMChannel;
    }
  }
}
