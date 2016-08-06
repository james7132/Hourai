using System;
using Discord;
using Discord.Commands.Permissions;

namespace DrumBot {

    public class BotOwnerChecker : IPermissionChecker {
        public bool CanRun(Discord.Commands.Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (!user.IsBotOwner()) {
                error = $"{user.Name} you are not the owner of this bot, and thus cannot run {command.Text}";
                return false;
            }
            return true;
        }
    }

    class PermissionChecker : IPermissionChecker {
        string PermissionName { get; }
        bool CheckBot { get; }
        bool CheckUser { get; }

        readonly Func<ServerPermissions, bool> PermCheck;

        public PermissionChecker(string name, 
                                 Func<ServerPermissions, bool> check, 
                                 bool checkBot = true, 
                                 bool checkUser = true) {
            PermissionName = Check.NotNull(name);
            PermCheck = Check.NotNull(check);
            CheckBot = checkBot;
            CheckUser = checkUser;
        }

        public bool CanRun(Discord.Commands.Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (CheckBot && !PermCheck(channel.Server.CurrentUser.ServerPermissions)) {
                error = $"{ channel.Server.CurrentUser.Name } does not have the { PermissionName.Code() } permission.";
                return false;
            }
            if (CheckUser && user.IsBotOwner())
                return true;
            if (CheckUser && !PermCheck(user.ServerPermissions)) {
                error = $"{user.Mention}, you do not have the { PermissionName.Code() } permission.";
                return false;
            }
            return true;
        }


    }

    public static class Check {
        public static T NotNull<T>(T obj) {
            if (obj == null)
                throw new ArgumentNullException();
            return obj;
        }

        public static IPermissionChecker ManageChannels(bool user = true, bool bot = true) => 
            new PermissionChecker("Manage Channels", s => s.ManageChannels, bot, user);

        public static IPermissionChecker ManageMessages(bool user = true, bool bot = true) => 
            new PermissionChecker("Manage Messages", s => s.ManageMessages, bot, user);

        public static IPermissionChecker ManageNicknames(bool user = true, bool bot = true) => 
            new PermissionChecker("Manage Nicknames", s => s.ManageNicknames, bot, user);

        public static IPermissionChecker ManageServer(bool user = true, bool bot = true) => 
            new PermissionChecker("Manage Server", s => s.ManageServer, bot, user);
        
        public static IPermissionChecker ManageRoles(bool user = true, bool bot = true) => 
            new PermissionChecker("Manage Roles", s => s.ManageRoles, bot, user);

        public static IPermissionChecker KickMembers(bool user = true, bool bot = true) => 
            new PermissionChecker("Kick Members", s => s.KickMembers, bot, user);

        public static IPermissionChecker BanMembers(bool user = true, bool bot = true) => 
            new PermissionChecker("Ban Members", s => s.BanMembers, bot, user);

        public static IPermissionChecker MuteMembers(bool user = true, bool bot = true) => 
            new PermissionChecker("Mute Members", s => s.MuteMembers, bot, user);

        public static IPermissionChecker DeafenMembers(bool user = true, bool bot = true) => 
            new PermissionChecker("Deafen Members", s => s.DeafenMembers, bot, user);
    }

}
