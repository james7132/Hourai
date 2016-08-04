using Discord;

namespace DrumBot {
    public class DeafenMembersChecker : PermissionChecker {
        protected override string PermissionName => "Deafen Members";
        protected override bool Check(User user) => user.ServerPermissions.DeafenMembers;
    }
}