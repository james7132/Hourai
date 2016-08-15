using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    public class PublicOnlyAttribute : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissions(
            IMessage context,
            Command executingCommand,
            object moduleInstance) {
            if (!(context.Channel is IGuildChannel))
                return PreconditionResult.FromError("Command must be executed in a public channel");
            return PreconditionResult.FromSuccess();
        }
    }

    public class PrivateOnlyAttribute : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissions(
            IMessage context,
            Command executingCommand,
            object moduleInstance) {
            if (!(context.Channel is IDMChannel))
                return PreconditionResult.FromError("Command must be executed in a private chat.");
            return PreconditionResult.FromSuccess();
        }
    }

    public class ModuleCheckAttribute : PublicOnlyAttribute {

        public string ModuleName { get; }
        public ModuleCheckAttribute(string moduleName) {
            ModuleName = moduleName;
        }

        public override async Task<PreconditionResult> CheckPermissions(
            IMessage context,
            Command executingCommand,
            object moduleInstance) {
            var baseCheck = await base.CheckPermissions(context, executingCommand, moduleInstance);
            if (!baseCheck.IsSuccess)
                return PreconditionResult.FromError(baseCheck);
            var config = Config.GetGuildConfig((context as IGuildChannel).Guild);
            if (config.IsModuleEnabled(ModuleName))
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError($"Module \"{ModuleName}\" is not enabled.");
        }
    }

    public class BotOwnerAttribute : PreconditionAttribute {
        public override async Task<PreconditionResult> CheckPermissions(
            IMessage context,
            Command executingCommand,
            object moduleInstance) {
            if (context.Author.IsBotOwner())
                return PreconditionResult.FromSuccess();
            return PreconditionResult.FromError("You must be the bot owner to use this command.");
        }
    }

    //    public class BotOwnerChecker : IPermissionChecker {
    //        public bool CanRun(Discord.CommandService.CommandUtility command,
    //                                    User user,
    //                                    Channel channel,
    //                                    out string error) {
    //            error = string.Empty;
    //            if (!user.IsBotOwner()) {
    //                error = $"{user.Name} you are not the owner of this bot, and thus cannot run {command.Text.Code()}";
    //                return false;
    //            }
    //            return true;
    //        }
    //    }

    //    public class ServerOwnerChecker : IPermissionChecker {
    //        public bool CanRun(Discord.CommandService.CommandUtility command,
    //                           User user,
    //                           Channel channel,
    //                           out string error) {
    //            error = string.Empty;
    //            if (!user.IsServerOwner()) {
    //                error = $"{user.Name} you are not the owner of this server, and thus cannot run {command.Text.Code()}";
    //                return false;
    //            }
    //            return true;
    //        }
    //    }

    //    class PermissionChecker : IPermissionChecker {
    //        string PermissionName { get; }
    //        bool CheckBot { get; }
    //        bool CheckUser { get; }

    //        readonly Func<ServerPermissions, bool> PermCheck;

    //        public PermissionChecker(string name, 
    //                                 Func<ServerPermissions, bool> check, 
    //                                 bool checkBot = true, 
    //                                 bool checkUser = true) {
    //            PermissionName = Check.NotNull(name);
    //            PermCheck = Check.NotNull(check);
    //            CheckBot = checkBot;
    //            CheckUser = checkUser;
    //        }

    //        public bool CanRun(Discord.CommandService.CommandUtility command,
    //                                    User user,
    //                                    Channel channel,
    //                                    out string error) {
    //            error = string.Empty;
    //            if (CheckBot && !PermCheck(channel.Server.CurrentUser.ServerPermissions)) {
    //                error = $"{ channel.Server.CurrentUser.Name } does not have the { PermissionName.Code() } permission.";
    //                return false;
    //            }
    //            if (CheckUser && user.IsBotOwner())
    //                return true;
    //            if (CheckUser && !PermCheck(user.ServerPermissions)) {
    //                error = $"{user.Mention}, you do not have the { PermissionName.Code() } permission.";
    //                return false;
    //            }
    //            return true;
    //        }


    //    }

    public static class Check {
        public static T NotNull<T>(T obj) {
            if (obj == null)
                throw new ArgumentNullException();
            return obj;
        }

        public static ITextChannel InGuild(IMessage message) {
            if(!(message.Channel is ITextChannel))
                throw new Exception("CommandUtility must be executed in a public channel");
            return message.Channel as ITextChannel;
        }

        public static IDMChannel InPrivate(IMessage message) {
            if(!(message.Channel is IDMChannel))
                throw new Exception("CommandUtility must be executed in a private channel");
            return message.Channel as IDMChannel;
        }
    }

}
