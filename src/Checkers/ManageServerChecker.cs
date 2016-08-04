using Discord;

namespace DrumBot {
    public class ManageServerChecker : PermissionChecker {
        protected override string PermissionName => "Manage Server";
        protected override bool Check(User user) => user.ServerPermissions.ManageServer; 
    }
}