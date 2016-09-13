using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    public class PublicOnlyAttribute : PreconditionAttribute {
        public override Task<PreconditionResult> CheckPermissions(
            IUserMessage context,
            Command executingCommand,
            object moduleInstance) {
            PreconditionResult result;
            if (!(context.Channel is IGuildChannel))
                result = PreconditionResult.FromError("Command must be executed in a public channel");
            else 
                result = PreconditionResult.FromSuccess();
            return Task.FromResult(result);
        }
    }

    public class PrivateOnlyAttribute : PreconditionAttribute {
        public override Task<PreconditionResult> CheckPermissions(
            IUserMessage context,
            Command executingCommand,
            object moduleInstance) {
            PreconditionResult result;
            if (!(context.Channel is IDMChannel))
                result = PreconditionResult.FromError("Command must be executed in a private chat.");
            else 
                result = PreconditionResult.FromSuccess();
            return Task.FromResult(result);
        }
    }

    public class ModuleCheckAttribute : PublicOnlyAttribute {
        public override async Task<PreconditionResult> CheckPermissions(
            IUserMessage context,
            Command executingCommand,
            object moduleInstance) {
            var baseCheck = await base.CheckPermissions(context, executingCommand, moduleInstance);
            if (!baseCheck.IsSuccess)
                return baseCheck;
            var config = Config.GetGuildConfig(QCheck.InGuild(context).Guild);
            if (config.IsModuleEnabled(executingCommand.Module.Name))
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError($"Module \"{executingCommand.Module.Name}\" is not enabled.");
        }
    }

    public class BotOwnerAttribute : PreconditionAttribute {
      public override Task<PreconditionResult> CheckPermissions(
          IUserMessage context,
          Command executingCommand,
          object moduleInstance) {
          PreconditionResult result;
          if (context.Author.IsBotOwner())
            result = PreconditionResult.FromSuccess();
          else 
            result = PreconditionResult.FromError("");
          return Task.FromResult(result);
      }
    }

    public enum Require {
        Bot,
        User,
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

        public override async Task<PreconditionResult> CheckPermissions(IUserMessage context, Command executingCommand, object moduleInstance) {
            // Check if the bot needs/has the permissions
            if(Requirement != Require.User) {
                IUser botUser = Bot.User;
                var guild = (context.Channel as IGuildChannel)?.Guild;
                if (guild != null)
                    botUser = await guild.GetCurrentUserAsync();
                var result = CheckUser(botUser, context.Channel);
                if(!result.IsSuccess)
                    return PreconditionResult.FromError(result);
            }
            // Check if the user has permissions. (Bot Owner gets override over this)
            if(Requirement != Require.Bot && !context.Author.IsBotOwner()) {
                var result = CheckUser(context.Author, context.Channel);
                if(!result.IsSuccess)
                    return PreconditionResult.FromError(result);
            }
            return PreconditionResult.FromSuccess();
        }
    }

    public class ServerOwnerAttribute : PreconditionAttribute {
        public override Task<PreconditionResult> CheckPermissions(
            IUserMessage context,
            Command executingCommand,
            object moduleInstance) {
            if (QCheck.InGuild(context) == null)
                return Task.FromResult(PreconditionResult.FromError("Not in server."));
            var user = context.Author as IGuildUser;
            if (!user.IsServerOwner())
                return Task.FromResult(PreconditionResult.FromError($"{user.Username} you are not the owner of this server, and thus cannot run {executingCommand.Text.Code()}"));
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }

    public class MinimumRoleAttribute : PreconditionAttribute {
        readonly string _roleType;

        public MinimumRoleAttribute(string roleType) { this._roleType = roleType; }

        public override async Task<PreconditionResult> CheckPermissions(
            IUserMessage context,
            Command executingCommand,
            object moduleInstance) {

            var user = context.Author as IGuildUser;
            if(user == null)
                return PreconditionResult.FromError("Must be in server to execute this command");
            if (user.IsBotOwner() || user.IsServerOwner())
                return PreconditionResult.FromSuccess();
            var server = user.Guild;
            var serverConfig = Config.GetGuildConfig(server);
            ulong? minRole = serverConfig.GetMinimumRole(_roleType);
            if (minRole == null)
                return PreconditionResult.FromError($"{user.Mention} is not the server owner, and no minimum role for {_roleType.Code()} is set.");
            var role = server.GetRole(minRole.Value);
            if (role == null) {
                var owner = await server.GetOwnerAsync();
                return PreconditionResult.FromError($"{owner.Mention} the role for {_roleType.Code()} no longer exists, and you are the only one who can now run it.");
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
