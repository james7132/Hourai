using Discord;
using Discord.Commands;

namespace DrumBot {
    public abstract class PermissionChecker : Checker {
        public override bool CanRun(Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (!Check(user)) {
                error =
                    $"{user.Mention}, you do not have the { PermissionName.Wrap("``")} permission.";
                return false;
            }
            if (!Check(channel.Server.GetUser(Bot.Client.CurrentUser.Id))) {
                error = $"{ Bot.Client.CurrentUser.Name } does not have the { PermissionName.Wrap("``") } permission.";
                return false;
            }
            return true;
        }

        protected abstract string PermissionName { get; }

        protected abstract bool Check(User user);

    }

    public class ManageRolesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Roles";
        protected override bool Check(User user) => user.ServerPermissions.ManageRoles;
    }

    public class KickMembersChecker : PermissionChecker {
        protected override string PermissionName => "Kick Members";
        protected override bool Check(User user) => user.ServerPermissions.KickMembers;
    }

    public class BanMembersChecker : PermissionChecker {
        protected override string PermissionName => "Ban Members";
        protected override bool Check(User user) => user.ServerPermissions.BanMembers; 
    }

    public class ManageChannelsChecker : PermissionChecker {
        protected override string PermissionName => "Manage Channels";
        protected override bool Check(User user) => user.ServerPermissions.ManageChannels; 
    }

    public abstract class UserPermissionChecker : Checker {
        public override bool CanRun(Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (user.IsBotOwner()) {
                Log.Info($"{user.Name} is BOT OWNER.");
                return true;
            }
            if (!Check(user)) {
                error =
                    $"{user.Mention}, you do not have the { PermissionName.Wrap("``")} permission.";
                return false;
            }
            return true;
        }

        protected abstract string PermissionName { get; }

        protected abstract bool Check(User user);

    }

    public class UserManageRolesChecker : UserPermissionChecker {
        protected override string PermissionName => "Manage Roles";
        protected override bool Check(User user) => user.ServerPermissions.ManageRoles;
    }

    public class UserKickMembersChecker : UserPermissionChecker {
        protected override string PermissionName => "Kick Members";
        protected override bool Check(User user) => user.ServerPermissions.KickMembers;
    }

    public class UserBanMembersChecker : UserPermissionChecker {
        protected override string PermissionName => "Ban Members";
        protected override bool Check(User user) => user.ServerPermissions.BanMembers; 
    }

    public class UserManageChannelsChecker : UserPermissionChecker {
        protected override string PermissionName => "Manage Channels";
        protected override bool Check(User user) => user.ServerPermissions.ManageChannels; 
    }
}
