using Discord;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

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

  public class RequirePermissionAttribute : DocumentedPreconditionAttribute {
    public Require Requirement { get; }
    public GuildPermission[] GuildPermission { get; }
    public ChannelPermission[] ChannelPermission { get; }

    public RequirePermissionAttribute(GuildPermission permission, Require requirement = Require.Both) {
      Requirement = requirement;
      GuildPermission = new [] {permission};
      ChannelPermission = null;
    }

    public RequirePermissionAttribute(ChannelPermission permission, Require requirement = Require.Both) {
      Requirement = requirement;
      ChannelPermission = new[] {permission};
      GuildPermission = null;
    }

    public RequirePermissionAttribute(GuildPermission[] permission, Require requirement = Require.Both) {
      Requirement = requirement;
      GuildPermission = permission;
      ChannelPermission = null;
    }

    public RequirePermissionAttribute(ChannelPermission[] permission, Require requirement = Require.Both) {
      Requirement = requirement;
      ChannelPermission = permission;
      GuildPermission = null;
    }

    public override string GetDocumentation() {
      var permissions = new HashSet<string>();
      if(GuildPermission != null)
        permissions.UnionWith(GuildPermission.Select(g => g.ToString().SplitCamelCase()));
      if(ChannelPermission != null)
        permissions.UnionWith(ChannelPermission.Select(g => g.ToString().SplitCamelCase()));
      if(permissions.Count <= 0)
        return string.Empty;
      var output = permissions.Select(s => s.Code());
      if(permissions.Count == 1)
        return $"Requires {output.First()} permission.";
      else
        return $"Requires {output.Join(", ")} permissions.";
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
        ICommandContext context,
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

}
