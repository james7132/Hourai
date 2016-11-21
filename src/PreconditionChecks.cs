using System;
using System.Threading.Tasks;
using Discord; 
using Discord.Commands; 

namespace Hourai {

  public enum ModuleType : long {
    Standard = 1 << 0,
    Admin = 1 << 1,
    Command = 1 << 2,
    Feeds = 1 << 3
  }

  public class ModuleCheckAttribute : RequireContextAttribute {

    public ModuleType Module { get; }

    public ModuleCheckAttribute(ModuleType module) : base(ContextType.Guild) {
      Module = module;
    }

    public override async Task<PreconditionResult> CheckPermissions(
        CommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      var baseCheck = await base.CheckPermissions(context, commandInfo, dependencies);
      if (!baseCheck.IsSuccess)
          return baseCheck;
      var guild = dependencies.Get<BotDbContext>().GetGuild(context.Guild);
      if (guild.IsModuleEnabled(Module))
          return PreconditionResult.FromSuccess();
      return PreconditionResult.FromError($"Module \"{commandInfo.Module.Name}\" is not enabled.");
    }
  }

  public class BotOwnerAttribute : PreconditionAttribute {
    public override Task<PreconditionResult> CheckPermissions(
        CommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      PreconditionResult result;
      if (context.Message.Author.IsBotOwner())
        result = PreconditionResult.FromSuccess();
      else 
        result = PreconditionResult.FromError("");
      return Task.FromResult(result);
    }
  }

  public enum Require {
      // Requires only the bot to have the permission
      Bot,
      // Requires only the user to have the permission
      User,
      // Requires both the bot and the user to have the permission
      // However provides a override for the bot owner
      BotOwnerOverride,
      // Requires both the bot and the user to have it.
      Both
  }

  public class Permission : PreconditionAttribute {
    public Require Requirement { get; }
    public GuildPermission[] GuildPermission { get; }
    public ChannelPermission[] ChannelPermission { get; }

    public Permission(GuildPermission permission, Require requirement = Require.Both) {
      Requirement = requirement;
      GuildPermission = new [] {permission};
      ChannelPermission = null;
    }

    public Permission(ChannelPermission permission, Require requirement = Require.Both) {
      Requirement = requirement;
      ChannelPermission = new[] {permission};
      GuildPermission = null;
    }

    public Permission(GuildPermission[] permission, Require requirement = Require.Both) {
      Requirement = requirement;
      GuildPermission = permission;
      ChannelPermission = null;
    }

    public Permission(ChannelPermission[] permission, Require requirement = Require.Both) {
      Requirement = requirement;
      ChannelPermission = permission;
      GuildPermission = null;
    }

    PreconditionResult CheckUser(IUser user, IChannel channel) {
      var guildUser = user as IGuildUser;
      
      // If user is server owner or has the administrator role
      // they get a free pass.
      if(guildUser != null && 
          (guildUser.IsServerOwner() || 
          guildUser.GuildPermissions
          .Has(Discord.GuildPermission.Administrator)))
        return PreconditionResult.FromSuccess();
      if (GuildPermission != null) {
        if (guildUser == null)
          return PreconditionResult.FromError("Command must be used in a guild channel");
        foreach (GuildPermission guildPermission in GuildPermission) {
          if (!guildUser.GuildPermissions.Has(guildPermission))
            return PreconditionResult.FromError($"Command requires guild permission {guildPermission.ToString().SplitCamelCase().Code()}");
        }
      }

      if (ChannelPermission != null) {
        var guildChannel = channel as IGuildChannel;
        ChannelPermissions perms;
        if (guildChannel != null)
          perms = guildUser.GetPermissions(guildChannel);
        else
          perms = ChannelPermissions.All(guildChannel);
        foreach (ChannelPermission channelPermission in ChannelPermission) {
          if (!perms.Has(channelPermission))
            return PreconditionResult.FromError($"Command requires channel permission {channelPermission.ToString().SplitCamelCase().Code()}");
        }
      }
      return PreconditionResult.FromSuccess();
    }

    public override async Task<PreconditionResult> CheckPermissions(
        CommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      // Check if the bot needs/has the permissions
      switch(Requirement) {
        case Require.Bot:
        case Require.Both:
          IUser botUser = Bot.User;
          var guild = (context.Channel as IGuildChannel)?.Guild;
          if (guild != null)
            botUser = await guild.GetCurrentUserAsync();
          var result = CheckUser(botUser, context.Channel);
          if(!result.IsSuccess)
            return PreconditionResult.FromError(result);
          break;
      }
      switch(Requirement) {
        case Require.User:
        case Require.BotOwnerOverride:
        case Require.Both:
          var author = context.Message.Author;
          if(Requirement == Require.BotOwnerOverride && context.User.IsBotOwner())
            break;
          var result = CheckUser(author, context.Channel);
          if(!result.IsSuccess)
            return PreconditionResult.FromError(result);
          break;
      }
      return PreconditionResult.FromSuccess();
    }
  }

  public class ServerOwnerAttribute : PreconditionAttribute {
    public override Task<PreconditionResult> CheckPermissions(
        CommandContext context,
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
        CommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      var user = context.Message.Author as IGuildUser;
      if(user == null)
        return PreconditionResult.FromError("Must be in server to execute this command");
      if (user.IsBotOwner() || user.IsServerOwner())
        return PreconditionResult.FromSuccess();
      var server = user.Guild;
      var guild = dependencies.Get<BotDbContext>().GetGuild(server);
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
