using Discord;

namespace DrumBot {
    public class BanMembersChecker : PermissionChecker {
        protected override string PermissionName => "Ban Members";
        protected override bool Check(User user) => user.ServerPermissions.BanMembers; 
    }
}