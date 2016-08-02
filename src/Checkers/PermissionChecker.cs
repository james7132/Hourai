using Discord;

namespace DrumBot {
    public abstract class PermissionChecker : Checker {
        public override bool CanRun(Discord.Commands.Command command,
                                    User user,
                                    Channel channel,
                                    out string error) {
            error = string.Empty;
            if (CheckUser && !Check(user)) {
                error =
                    $"{user.Mention}, you do not have the { PermissionName.Wrap("``")} permission.";
                return false;
            }
            if (CheckBot && !Check(channel.Server.CurrentUser)) {
                error = $"{ Bot.Client.CurrentUser.Name } does not have the { PermissionName.Wrap("``") } permission.";
                return false;
            }
            return true;
        }

        protected abstract string PermissionName { get; }

        protected virtual bool CheckBot { get; } = true;
        protected virtual bool CheckUser { get; } = true;

        protected abstract bool Check(User user);

    }


    #region Manage Channels
    public class ManageChannelsChecker : PermissionChecker {
        protected override string PermissionName => "Manage Channels";
        protected override bool Check(User user) => user.ServerPermissions.ManageChannels; 
    }

    public class UserManageChannelsChecker : ManageChannelsChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotManageChannelsChecker : ManageChannelsChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Manage Messages
    public class ManageMessagesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Messages";
        protected override bool Check(User user) => user.ServerPermissions.ManageMessages; 
    }

    public class UserManageMessagesChecker : ManageMessagesChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotManageMessagesChecker : ManageMessagesChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Manage Nicknames
    public class ManageNicknamesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Nicknames";
        protected override bool Check(User user) => user.ServerPermissions.ManageNicknames; 
    }

    public class UserManageNicknamesChecker : ManageNicknamesChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotManageNicknamesChecker : ManageNicknamesChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion
 
    #region Manage Server
    public class ManageServerChecker : PermissionChecker {
        protected override string PermissionName => "Manage Server";
        protected override bool Check(User user) => user.ServerPermissions.ManageServer; 
    }

    public class UserManageServerChecker : ManageServerChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotManageServerChecker : ManageServerChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Manage Roles
    public class ManageRolesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Roles";
        protected override bool Check(User user) => user.ServerPermissions.ManageRoles;
    }

    public class UserManageRolesChecker : ManageRolesChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotManageRolesChecker : ManageRolesChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Kick Members
    public class KickMembersChecker : PermissionChecker {
        protected override string PermissionName => "Kick Members";
        protected override bool Check(User user) => user.ServerPermissions.KickMembers;
    }

    public class UserKickMembersChecker : KickMembersChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotKickMembersChecker : KickMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Mute Members
    public class MuteMembersChecker : PermissionChecker {
        protected override string PermissionName => "Mute Members";
        protected override bool Check(User user) => user.ServerPermissions.MuteMembers;
    }

    public class UserMuteMembersChecker : MuteMembersChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotMuteMembersChecker : MuteMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Deafen Members
    public class DeafenMembersChecker : PermissionChecker {
        protected override string PermissionName => "Deafen Members";
        protected override bool Check(User user) => user.ServerPermissions.DeafenMembers;
    }

    public class UserDeafenMembersChecker : DeafenMembersChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotDeafenMembersChecker : DeafenMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion

    #region Ban Members
    public class BanMembersChecker : PermissionChecker {
        protected override string PermissionName => "Ban Members";
        protected override bool Check(User user) => user.ServerPermissions.BanMembers; 
    }

    public class UserBanMembersChecker : BanMembersChecker {
        protected override bool CheckBot { get; } = false;
    }

    public class BotBanMembersChecker : BanMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
    #endregion


}
