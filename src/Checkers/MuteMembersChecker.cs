using Discord;

namespace DrumBot {
    public class MuteMembersChecker : PermissionChecker {
        protected override string PermissionName => "Mute Members";
        protected override bool Check(User user) => user.ServerPermissions.MuteMembers;
    }
}