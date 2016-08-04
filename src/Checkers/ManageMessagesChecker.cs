using Discord;

namespace DrumBot {
    public class ManageMessagesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Messages";
        protected override bool Check(User user) => user.ServerPermissions.ManageMessages; 
    }
}