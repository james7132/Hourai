using Discord;

namespace DrumBot {
    public class KickMembersChecker : PermissionChecker {
        protected override string PermissionName => "Kick Members";
        protected override bool Check(User user) => user.ServerPermissions.KickMembers;
    }
}