using Discord;

namespace DrumBot {
    public class ManageNicknamesChecker : PermissionChecker {
        protected override string PermissionName => "Manage Nicknames";
        protected override bool Check(User user) => user.ServerPermissions.ManageNicknames; 
    }
}