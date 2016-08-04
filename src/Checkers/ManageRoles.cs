using Discord;

namespace DrumBot {
    public class ManageRoles : PermissionChecker {
        protected override string PermissionName => "Manage Roles";
        protected override bool Check(User user) => user.ServerPermissions.ManageRoles;
    }
}